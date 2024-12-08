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

using Pfs.Helpers;
using Pfs.Data.Stalker;
using Pfs.Types;
using System.Collections.Immutable;
using System.Text;
using Serilog;

namespace Pfs.Client;

// Provides access/storing of Stalker data, and provides API for FE to access it
public class ClientStalker : StalkerDoCmd, ICmdHandler, IDataOwner
{   // Note! 'StalkerDoCmd' is derived to get access to internals used for storing etc
    protected const string _componentName = "stalker";

    protected IPfsPlatform _platform;

    public ClientStalker(IPfsPlatform platform)
    {
        _platform = platform;

        Init();
    }

    protected new void Init()
    {
        base.Init();
    }

    protected readonly static ImmutableArray<string> _cmdTemplates = [
        "list",
        "help",
        "pflist",
        "pfstocks pf",
        "cmd action"
    ];

    public new Result DoAction(string cmd)
    {
        Result resp = base.DoAction(cmd);

        if (resp.Ok == false)
            return resp;
            
        EventNewUnsavedContent?.Invoke(this, _componentName);

        return resp;
    }

    public event EventHandler<string> EventNewUnsavedContent;          // IDataOwner
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

            (StalkerXML.Imported data, warnings) = StalkerXML.ImportXml(xml);

            _portfolios = data.Portfolios;
            _stocks = data.Stocks;
            _sectors = data.Sectors;
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
        return StalkerXML.ExportXml(this);
    }

    public string CreatePartialBackup(List<string> symbols)
    {
        return StalkerXML.ExportXml(this, symbols);
    }

    public List<string> RestoreBackup(string content)
    {
        try
        {
            (StalkerXML.Imported data, List<string> warnings) = StalkerXML.ImportXml(content);

            _portfolios = data.Portfolios;
            _stocks = data.Stocks;
            _sectors = data.Sectors;

            return warnings;
        }
        catch (Exception ex)
        {
            List<string> warnings = new();
            string wrnmsg = $"{_componentName}, failed to load stalker data w exception [{ex.Message}]";
            warnings.Add(wrnmsg);
            Log.Warning(wrnmsg);
            return warnings;
        }
    }

    protected void BackupToStorage()
    {
        _platform.PermWrite(_componentName, StalkerXML.ExportXml(this));
    }

    public string GetCmdPrefixes() { return _componentName; }                                       // ICmdHandler

    public async Task<Result<string>> CmdAsync(string cmd)                                          // ICmdHandler
    {
        await Task.CompletedTask;

        StringBuilder sb = new();
        var parseResp = CmdParser.Parse(cmd, _cmdTemplates);

        if (parseResp.Fail) // parser gives per templates a proper fail w help
            return new FailResult<string>((parseResp as FailResult<Dictionary<string, string>>).Message);

        switch (parseResp.Data["cmd"])
        {
            case "list":
                return new OkResult<string>("todo ClientStalker");

            case "help":
                return new OkResult<string>(StalkerAction.Help());

            case "pflist":
                {
                    foreach (SPortfolio pf in Portfolios())
                        sb.AppendLine(pf.Name);

                    return new OkResult<string>(sb.ToString());
                }

            case "pfstocks":
                {
                    SPortfolio pf = PortfolioRef(parseResp.Data["pf"]);

                    if (pf == null)
                        return new FailResult<string>($"{parseResp.Data["pf"]} is not known portfolio!");

                    foreach (string sRef in pf.SRefs)
                        sb.AppendLine(sRef);

                    return new OkResult<string>(sb.ToString());
                }

            case "cmd":
                {
                    string stalkerCmd = parseResp.Data["action"];

                    Result actionRes = DoAction(stalkerCmd);

                    if (actionRes.Ok) 
                        return new OkResult<string>($"OK: {stalkerCmd}");
                    else
                        return new FailResult<string>($"Failed: {(actionRes as FailResult).Message}");
                }
        }

        throw new NotImplementedException($"ClientStalker.CmdAsync missing {parseResp.Data["cmd"]}");
    }

    public async Task<Result<string>> HelpMeAsync(string cmd)                                       // ICmdHandler
    {
        await Task.CompletedTask;

        var parseResp = CmdParser.Parse(cmd, _cmdTemplates);

        if (parseResp.Fail) // parser gives per templates a proper fail w help
            return new FailResult<string>((parseResp as FailResult<Dictionary<string, string>>).Message);

        return new OkResult<string>("Cmd is OK:" + string.Join(",", parseResp.Data.Select(x => x.Key + "=" + x.Value)));
    }
}
