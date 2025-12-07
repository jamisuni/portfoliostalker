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

using System.Text;
using System.Xml.Linq;
using System.Collections.Immutable;

using Pfs.Helpers;
using Pfs.Types;
using Serilog;

namespace Pfs.Config;

public class AppConfig : ICmdHandler, IDataOwner // identical XML on backup & local storage
{
    protected const string _componentName = "cfgapp";

    protected IPfsPlatform _platform;

    protected readonly static ImmutableArray<string> _cmdTemplates = [
        "list",
        "set <appcfg> int",
        "setcol <appcfg> <extracolumn>",
        "reset <appcfg>",
        "resetall"
    ];

    protected record AppCfgDef(int def);

    // Premium Providers! Can set from terminal below DayCredits,MonthCredits,Speed limites less restrictive

    protected static readonly ImmutableDictionary<AppCfgId, AppCfgDef> _cfgDef = new Dictionary<AppCfgId, AppCfgDef>
    { 
        { AppCfgId.AlphaVantageDayCredits,          new AppCfgDef(25) },
        { AppCfgId.AlphaVantageMonthCredits,        new AppCfgDef(0) },
        { AppCfgId.AlphaVantageSpeedSecs,           new AppCfgDef(13) },    // 60sec/5times = 12sec theory

        { AppCfgId.PolygonDayCredits,               new AppCfgDef(0) },
        { AppCfgId.PolygonMonthCredits,             new AppCfgDef(0) },
        { AppCfgId.PolygonSpeedSecs,                new AppCfgDef(13) },    // 60sec/5times = 12sec theory

        { AppCfgId.TwelveDataDayCredits,            new AppCfgDef(800) },
        { AppCfgId.TwelveDataMonthCredits,          new AppCfgDef(0) },
        { AppCfgId.TwelveDataSpeedSecs,             new AppCfgDef(8) },     // 60sec/8times = 7.5sec theory

        { AppCfgId.UnibitDayCredits,                new AppCfgDef(0) },
        { AppCfgId.UnibitMonthCredits,              new AppCfgDef(50000) },

        { AppCfgId.MarketstackDayCredits,           new AppCfgDef(0) },
        { AppCfgId.MarketstackMonthCredits,         new AppCfgDef(10000) }, // with 9$ per month

        { AppCfgId.FMPDayCredits,                   new AppCfgDef(250) },
        { AppCfgId.FMPMonthCredits,                 new AppCfgDef(0) },
        { AppCfgId.FMPSpeedSecs,                    new AppCfgDef(0) },

        { AppCfgId.EodHDDayCredits,                 new AppCfgDef(300) },
        { AppCfgId.EodHDMonthCredits,               new AppCfgDef(0) },
        { AppCfgId.EodHDSpeedSecs,                  new AppCfgDef(0) },

        //        { AppCfgId.TiingoDayCredits,                new AppCfgDef(0) },
        //        { AppCfgId.TiingoMonthCredits,              new AppCfgDef(0) },
        //        { AppCfgId.TiingoSpeedSecs,                 new AppCfgDef(0) },

        { AppCfgId.ExtraColumn0,                    new AppCfgDef((int)ExtraColumnId.CloseWeekAgo) },
        { AppCfgId.ExtraColumn1,                    new AppCfgDef((int)ExtraColumnId.CloseMonthAgo) },
        { AppCfgId.ExtraColumn2,                    new AppCfgDef((int)ExtraColumnId.Unknown) },
        { AppCfgId.ExtraColumn3,                    new AppCfgDef((int)ExtraColumnId.Unknown) },

        { AppCfgId.HideCompanyName,                 new AppCfgDef(0) },     // 0 == false, 1 == true

        { AppCfgId.OverviewStockAmount,             new AppCfgDef(15) },

        { AppCfgId.HoldingLvlPeriod,                new AppCfgDef(5) },     // 0 == off, def 5 days

        { AppCfgId.DefTrailingSellP,                new AppCfgDef(7) },     // def 7% from top is sell alarm

        { AppCfgId.DefTrailingBuyP,                 new AppCfgDef(14) },    // def 14% recovery from bottom as buy alarm

        { AppCfgId.UseBetaFeatures,                 new AppCfgDef(0) },     // 0 == off, 1 is on

        { AppCfgId.IOwn,                            new AppCfgDef(0) },

    }.ToImmutableDictionary();

    protected Dictionary<AppCfgId, int> _configs = new();

    public AppConfig(IPfsPlatform platform)
    {
        _platform = platform;

        Init();
    }

    protected void Init()
    {
        _configs = new();
    }

    public int Get(AppCfgId id)
    {
        if (_configs.ContainsKey(id) )
            return _configs[id];

        return _cfgDef[id].def;
    }

    protected void Set(AppCfgId id, int? val = null)
    {
        if ( val == null || val == _cfgDef[id].def)
        {   // Back to default, so no need have value
            if (_configs.ContainsKey(id))
                _configs.Remove(id);
            return;
        }

        if (_configs.ContainsKey(id))
            _configs[id] = val.Value;
        else
            _configs.Add(id, val.Value);
    }

    public event EventHandler<string> EventNewUnsavedContent;                                       // IDataOwner
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
        return ExportXml();
    }

    public List<string> RestoreBackup(string content)
    {
        return ImportXml(content);
    }

    protected void BackupToStorage()
    {
        _platform.PermWrite(_componentName, ExportXml());
    }

    protected string ExportXml()
    {
        XElement rootPFS = new XElement("PFS");

        // Keep this separate as so much more critical information
        XElement allUserCfgElem = new XElement("AppCfg");
        rootPFS.Add(allUserCfgElem);

        foreach (KeyValuePair<AppCfgId, int> ac in _configs)
        {
            XElement acElem = new XElement(ac.Key.ToString());

            acElem.SetAttributeValue("Value", ac.Value);

            allUserCfgElem.Add(acElem);
        }

        return rootPFS.ToString();
    }

    protected List<string> ImportXml(string xml)
    {
        List<string> warnings = new();
        Dictionary<AppCfgId, int> cfgs = new();

        try
        {
            XDocument xmlDoc = XDocument.Parse(xml);
            XElement rootPFS = xmlDoc.Element("PFS");

            XElement allUserCfgElem = rootPFS.Element("AppCfg");

            foreach (XElement cfgElem in allUserCfgElem.Elements())
            {
                AppCfgId id = (AppCfgId)Enum.Parse(typeof(AppCfgId), cfgElem.Name.ToString());

                int value = (int)cfgElem.Attribute("Value");

                if (value != _cfgDef[id].def)
                    cfgs.Add(id, value);
            }
        }
        catch (Exception ex) {
            string wrnmsg = $"{_componentName}, failed to load app configs w exception [{ex.Message}]";
            warnings.Add(wrnmsg);
            Log.Warning(wrnmsg);
        }
        _configs = cfgs;
        return warnings;
    }

    public string GetCmdPrefixes() { return _componentName; }                   // ICmdHandler

    public async Task<Result<string>> CmdAsync(string cmd)                      // ICmdHandler
    {
        await Task.CompletedTask;

        var parseResp = CmdParser.Parse(cmd, _cmdTemplates);

        if (parseResp.Fail) // parser gives per templates a proper fail w help
            return new FailResult<string>((parseResp as FailResult<Dictionary<string, string>>).Message);

        switch (parseResp.Data["cmd"])
        {
            case "list":
                {
                    StringBuilder sb = new();
                    foreach (AppCfgId id in Enum.GetValues(typeof(AppCfgId)))
                    {
                        if (_configs.ContainsKey(id))
                            sb.AppendLine($"{id} = {_configs[id]} def {_cfgDef[id].def}");
                        else
                            sb.AppendLine($"{id} = def {_cfgDef[id]}");
                    }
                    return new OkResult<string>(sb.ToString());
                }

            case "reset": // "reset <appcfg>"
                {
                    AppCfgId id = Enum.Parse<AppCfgId>(parseResp.Data["<appcfg>"]);
                    Set(id);
                    EventNewUnsavedContent?.Invoke(this, _componentName);
                    return new OkResult<string>($"{id} = {Get(id)}");
                }

            case "set": // "set <appcfg> int"
                {
                    AppCfgId id = Enum.Parse<AppCfgId>(parseResp.Data["<appcfg>"]);
                    if (int.TryParse(parseResp.Data["int"], out int value))
                    {
                        Set(id, value);
                        EventNewUnsavedContent?.Invoke(this, _componentName);
                        return new OkResult<string>($"{id} = {Get(id)}");
                    }
                    return new FailResult<string>("Cmd parsing failed (int required)");
                }

            case "setcol": //"setcol <appcfg> <extracolumn>"
                {
                    AppCfgId id = Enum.Parse<AppCfgId>(parseResp.Data["<appcfg>"]);
                    ExtraColumnId colId = Enum.Parse<ExtraColumnId>(parseResp.Data["<extracolumn>"]);
                    Set(id, (int)colId);
                    EventNewUnsavedContent?.Invoke(this, _componentName);
                    return new OkResult<string>($"{id} = {Get(id)}");
                }

            case "resetall":
                Init();
                EventNewUnsavedContent?.Invoke(this, _componentName);
                return new OkResult<string>("All restored back to defaults");
        }
        throw new NotImplementedException($"AppConfig.CmdAsync missing {parseResp.Data["cmd"]}");
    }

    public async Task<Result<string>> HelpMeAsync(string cmd)                   // ICmdHandler
    {
        await Task.CompletedTask;

        var parseResp = CmdParser.Parse(cmd, _cmdTemplates);

        if (parseResp.Fail) // parser gives per templates a proper fail w help
            return new FailResult<string>((parseResp as FailResult<Dictionary<string, string>>).Message);

        return new OkResult<string>("Cmd is OK:" + string.Join(",", parseResp.Data.Select(x => x.Key + "=" + x.Value)));
    }
}
