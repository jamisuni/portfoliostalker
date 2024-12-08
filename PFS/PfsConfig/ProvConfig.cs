/*
 * Copyright (C) 2024 Jami Suni
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/gpl-3.0.en.html>.
 */

using System.Collections.Immutable;
using System.Text;
using System.Xml.Linq;

using Pfs.Helpers;
using Pfs.Types;
using Serilog;

namespace Pfs.Config;

// Stores provider keys and provides access to provider's status and their configured private keys
public class ProvConfig : IPfsProvConfig, ICmdHandler, IDataOwner // identical XML on backup & local storage
{
    public const string _componentName = "cfgprov";
    protected IPfsPlatform _platform;

    public event EventHandler<ExtProviderId> EventProvConfigsChanged;       // IPfsProvConfig

    protected Dictionary<ExtProviderId, string> _configs = new();

    protected readonly static ImmutableArray<string> _cmdTemplates = [
        "list",
        "setkey <provider> key", 
        "delkey <provider>", 
        "clearall"
    ];

    public ProvConfig(IPfsPlatform platform)
    {
        _platform = platform;

        Init();
    }

    protected void Init()
    {
        _configs = new();
    }

    protected void OnEventProvConfigsChanged(ExtProviderId id)
    {
        EventHandler<ExtProviderId> handler = EventProvConfigsChanged;
        if (handler != null)
            handler(this, id);
    }

    public void SetPrivateKey(ExtProviderId provider, string privateKey)
    {
        if ( string.IsNullOrWhiteSpace(privateKey))
        {
            if (_configs.ContainsKey(provider) )
                _configs.Remove(provider);
        }
        else
        {
            if (_configs.ContainsKey(provider) )
                _configs[provider] = privateKey;
            else
                _configs.Add(provider, privateKey);
        }

        OnEventProvConfigsChanged(provider);
        EventNewUnsavedContent?.Invoke(this, _componentName);
    }

    public string GetPrivateKey(ExtProviderId provider)                     // IPfsProvConfig
    {
        if (_configs.ContainsKey(provider) == false)
            return string.Empty;

        return _configs[provider];
    }

    public List<ExtProviderId> GetActiveProviders()                         // IPfsProvConfig
    {
        return _configs.Keys.ToList();
    }

    public string GetXmlWithHiddenKeys()
    {
        return ExportXml(false /*includeKeys*/);
    }

    public event EventHandler<string> EventNewUnsavedContent;               // IDataOwner
    public string GetComponentName() { return _componentName; }
    public void OnInitDefaults() { Init(); }

    public List<string> OnLoadStorage()
    {
        List<string> warnings = new();

        try
        {
            Init();

            string xml = _platform.PermRead(_componentName);

            if (string.IsNullOrWhiteSpace(xml))
                return new();

            warnings = ImportXml(xml);
        }
        catch (Exception ex)
        {
            string wrnmsg = $"{_componentName}, OnLoadStorage failed w exception [{ex.Message}]";
            warnings.Add(wrnmsg);
            Log.Warning(wrnmsg);
        }
        return warnings;
    }

    public void OnSaveStorage() { BackupToStorage(); }

    public string CreateBackup()
    {
        return ExportXml();
    }

    public string CreatePartialBackup(List<string> symbols)
    {
        return string.Empty;
    }

    public List<string> RestoreBackup(string content)
    {
        return ImportXml(content);
    }

    protected void BackupToStorage()
    {
        _platform.PermWrite(_componentName, ExportXml());
    }

    protected string ExportXml(bool includeKeys = true)
    {
        XElement rootPFS = new XElement("PFS");

        // Keep this separate as so much more critical information
        XElement allProvElem = new XElement("Providers");
        rootPFS.Add(allProvElem);

        foreach (KeyValuePair<ExtProviderId, string> pc in _configs)
        {
            if (string.IsNullOrWhiteSpace(pc.Value))
                continue;

            XElement pcElem = new XElement(pc.Key.ToString());

            if ( includeKeys )
                pcElem.SetAttributeValue("PrivKey", pc.Value);
            else // when doing local dump thats kind of 'send to developer' file.. leaving exact keys off
                pcElem.SetAttributeValue("PrivKey", $"Key len={pc.Value.Length} as [{new string(pc.Value.Take(3).ToArray())}...{new string(pc.Value.TakeLast(3).ToArray())}]");

            allProvElem.Add(pcElem);
        }

        return rootPFS.ToString();
    }

    protected List<string> ImportXml(string xml)
    {
        List<string> warnings = new();
        Dictionary<ExtProviderId, string> cfgs = new();

        try
        { 
            XDocument xmlDoc = XDocument.Parse(xml);
            XElement rootPFS = xmlDoc.Element("PFS");

            XElement allProvElem = rootPFS.Element("Providers");
            if (allProvElem != null && allProvElem.HasElements)
            {
                foreach (XElement pcElem in allProvElem.Elements())
                {
                    if (Enum.TryParse(pcElem.Name.ToString(), out ExtProviderId provId) == false ||
                        pcElem.Attribute("PrivKey") == null)
                        continue;

                    cfgs.Add(provId, (string)pcElem.Attribute("PrivKey"));
                }
            }
        }
        catch (Exception ex) {
            string wrnmsg = $"{_componentName}, failed to load private key configs w exception [{ex.Message}]";
            warnings.Add(wrnmsg);
            Log.Warning(wrnmsg);
        }
        _configs = cfgs;
        return warnings;
    }

    public string GetCmdPrefixes() { return _componentName; }               // ICmdHandler

    public async Task<Result<string>> CmdAsync(string cmd)                  // ICmdHandler
    {
        await Task.CompletedTask;

        ExtProviderId provId;
        var parseResp = CmdParser.Parse(cmd, _cmdTemplates);

        if (parseResp.Fail) // parser gives per templates a proper fail w help
            return new FailResult<string>((parseResp as FailResult<Dictionary<string, string>>).Message);

        switch (parseResp.Data["cmd"])
        {
            case "list":
                StringBuilder list = new();

                foreach (ExtProviderId value in Enum.GetValues(typeof(ExtProviderId)))
                {
                    if (value == ExtProviderId.Unknown)
                        continue;

                    if (_configs.ContainsKey(value) )
                        list.AppendLine($"{value} on={_configs[value]}");
                    else
                        list.AppendLine($"{value} -not active-");
                }
                return new OkResult<string>(list.ToString());

            case "setkey":
                provId = Enum.Parse<ExtProviderId>(parseResp.Data["<provider>"]);
                SetPrivateKey(provId, parseResp.Data["key"]);
                return new OkResult<string>($"{provId} key updated!");

            case "delkey":
                provId = Enum.Parse<ExtProviderId>(parseResp.Data["<provider>"]);
                SetPrivateKey(provId, null);
                return new OkResult<string>($"{provId} key removed!");

            case "clearall":
                List<ExtProviderId> keepCopy = _configs.Keys.ToList();
                Init();
                foreach (ExtProviderId id in keepCopy)
                    OnEventProvConfigsChanged(id);
                return new OkResult<string>($"All keys removed!");
        }

        throw new NotImplementedException($"ProvConfig.CmdAsync missing {parseResp.Data["cmd"]}");
    }

    public async Task<Result<string>> HelpMeAsync(string cmd)               // ICmdHandler
    {
        await Task.CompletedTask;

        var parseResp = CmdParser.Parse(cmd, _cmdTemplates);

        if (parseResp.Fail) // parser gives per templates a proper fail w help
            return new FailResult<string>((parseResp as FailResult<Dictionary<string, string>>).Message);

        return new OkResult<string>("Cmd is OK:" + string.Join(",", parseResp.Data.Select(x => x.Key + "=" + x.Value)));
    }
}
