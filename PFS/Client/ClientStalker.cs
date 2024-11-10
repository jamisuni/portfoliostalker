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
using System.Text.Json;
using Serilog;

namespace Pfs.Client;

// Provides access/storing of Stalker data, and provides API for FE to access it
public class ClientStalker : StalkerDoCmd, ICmdHandler, IDataOwner
{   // Note! 'StalkerDoCmd' is derived to get access to internals used for storing etc
    protected const string _componentName = "stalker";
    protected const string storagePortfoliosKey = "PortfoliosJSON";
    protected const string storageStocksKey = "StocksJSON";
    protected const string storageSectorsKey = "SectorsJSON";

    protected IPfsPlatform _platform;

    public ClientStalker(IPfsPlatform platform)
    {
        _platform = platform;

        LoadStorageContent();
    }

    protected void Init()
    {
        _portfolios = new();
        _stocks = new();
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
    public void OnDataInit() { Init(); }
    public void OnDataSaveStorage() { BackupToStorage(); }

    public string CreateBackup()
    {
        return StalkerXML.ExportXml(this);
    }

    public string CreatePartialBackup(List<string> symbols)
    {
        return StalkerXML.ExportXml(this, symbols);
    }

    public Result RestoreBackup(string content)
    {
        try
        {
            (StalkerXML.Imported data, List<string> warnings) = StalkerXML.ImportXml(content);

            _portfolios = data.Portfolios;
            _stocks = data.Stocks;
            _sectors = data.Sectors;

            return new OkResult(); // !!!TODO!!! No more result, instead warnings.. and no more autom wipe..
        }
        catch ( Exception ex)
        {
            Log.Warning($"{_componentName} RestoreBackup failed to exception: [{ex.Message}]");
            return new FailResult($"ClientStalker: Exception: {ex.Message}");
        }
    }

    protected void BackupToStorage()
    {
        string portfoliosJSON = JsonSerializer.Serialize(_portfolios);
        _platform.PermWrite(storagePortfoliosKey, portfoliosJSON);

        string stocksJSON = JsonSerializer.Serialize(_stocks);
        _platform.PermWrite(storageStocksKey, stocksJSON);

        string sectorsJSON = JsonSerializer.Serialize(_sectors);
        _platform.PermWrite(storageSectorsKey, sectorsJSON);
    }

    protected void LoadStorageContent()
    {
        try
        {
            Init();

            string portfoliosJSON = _platform.PermRead(storagePortfoliosKey);

            if (string.IsNullOrEmpty(portfoliosJSON) == false)
                _portfolios = JsonSerializer.Deserialize<List<SPortfolio>>(portfoliosJSON);

            string stocksJSON = _platform.PermRead(storageStocksKey);

            if (string.IsNullOrEmpty(stocksJSON) == false)
                _stocks = JsonSerializer.Deserialize<List<SStock>>(stocksJSON);

            string sectorsJSON = _platform.PermRead(storageSectorsKey);

            if (string.IsNullOrEmpty(sectorsJSON) == false)
                _sectors = JsonSerializer.Deserialize<SSector[]>(sectorsJSON);
        }
        catch (Exception ex)
        {
            Log.Warning($"{_componentName} LoadStorageContent failed to exception: [{ex.Message}]");
            Init();
            _platform.PermRemove(storagePortfoliosKey);
            _platform.PermRemove(storageStocksKey);
            _platform.PermRemove(storageSectorsKey);
        }
    }

    public string GetCmdPrefixes() { return _componentName; }                                       // ICmdHandler

    public async Task<Result<string>> CmdAsync(string cmd)                                          // ICmdHandler
    {
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
        var parseResp = CmdParser.Parse(cmd, _cmdTemplates);

        if (parseResp.Fail) // parser gives per templates a proper fail w help
            return new FailResult<string>((parseResp as FailResult<Dictionary<string, string>>).Message);

        return new OkResult<string>("Cmd is OK:" + string.Join(",", parseResp.Data.Select(x => x.Key + "=" + x.Value)));
    }
}
