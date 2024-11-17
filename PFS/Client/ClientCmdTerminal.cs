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

using Pfs.Types;
using Pfs.Helpers;
using Pfs.ExtFetch;
using System.Collections.ObjectModel;

namespace Pfs.Client;

// Gets all DI registered ICmdHandler's, asks their component names, and then
// behaves as forward point of passing cmds/helpMe's to specific component
public class ClientCmdTerminal : IFECmdTerminal
{
    protected readonly Dictionary<string, ICmdHandler> _handlers;
    protected readonly string _handlersHelp;
    protected readonly IFetchRates _ratesProv;
    protected readonly IStockMeta _stockMetaProv;
    protected readonly IStockMetaUpdate _stockMetaUpdate;
    protected readonly ClientStalker _clientStalker;
    protected readonly ILatestRates _latestRatesProv;
    protected readonly ClientData _clientData;

    protected readonly static ImmutableArray<string> _cmdTemplates = [
        "list",
        "reqrates",
        "stock sref",
        "destroystock sref",
        "sectors",
        "partialbackupzip symbols filename",
        "storagedebugzip"
    ];

    public ClientCmdTerminal(IEnumerable<ICmdHandler> handlers, IFetchRates ratesProv, IStockMeta stockMetaProv, IStockMetaUpdate stockMetaUpdate, ClientStalker clientStalker, ILatestRates latestRatesProv, ClientData clientData)
    {
        _ratesProv = ratesProv;
        _stockMetaProv = stockMetaProv;
        _stockMetaUpdate = stockMetaUpdate;
        _clientStalker = clientStalker;
        _latestRatesProv = latestRatesProv;
        _clientData = clientData;

        _handlers = new(); // Create dictionary w command prefix as key, and related handler as value
        List<string> allHandlers = new();
        foreach (var hndlr in handlers)
        {
            string[] prefixes = hndlr.GetCmdPrefixes().Split(',');
            allHandlers.Add(prefixes[0]);
            foreach (string prefix in prefixes)
                _handlers.Add(prefix, hndlr);
        }
        _handlersHelp = string.Join(',', allHandlers);
    }

    public async Task<Result<string>> CmdAsync(string cmd)
    {
        string[] split = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (split.Length > 1 && split[0] == "client")
            return await ClientCmdAsync(split);

        if (split.Count() == 0 || _handlers.ContainsKey(split[0]) == false)
            return new FailResult<string>($"Unknown component! [comps,{_handlersHelp}]");

        // Cmd does perform operation
        return await _handlers[split[0]].CmdAsync(cmd.Substring(split[0].Length + 1));
    }

    public async Task<Result<string>> HelpMeAsync(string cmd)
    {
        string[] split = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (split.Length >= 1 && split[0] == "client")
            return await ClientHelpMeAsync(cmd.Substring(split[0].Length + 1));

        if (split.Count() == 0 || _handlers.ContainsKey(split[0]) == false)
            return new FailResult<string>($"Unknown component! [comps,client,{_handlersHelp}]");

        // HelpMe only provides information if command is not ready, or OK if looks ready
        return await _handlers[split[0]].HelpMeAsync(cmd.Substring(split[0].Length + 1));
    }

    protected async Task<Result<string>> ClientCmdAsync(string[] split)                             // <== these are commands under 'client' 
    {
        StringBuilder sb = new();

        switch (split[1])
        {
            case "list":
                {
                    sb.AppendLine("***");
                    foreach (KeyValuePair<string, ICmdHandler> kvp in _handlers)
                    {
                        sb.AppendLine($"*{kvp.Key}");

                        Result<string> subResp = await kvp.Value.CmdAsync("list");

                        if (subResp.Ok)
                            sb.AppendLine(subResp.Data);
                        else
                            sb.AppendLine((subResp as FailResult<string>).Message);
                    }
                    sb.AppendLine("***");
                }
                return new OkResult<string>(sb.ToString());

            case "reqrates":
                {
                    Result resp = _ratesProv.FetchLatest(_latestRatesProv.HomeCurrency);

                    if ( resp.Ok)
                        return new OkResult<string>("Requested");
                    else
                        return new FailResult<string>((resp as FailResult).Message); 
                }

            case "stock":
                {
                    sb.AppendLine($"*** {split[2]}");

                    StockMeta stockMeta = _stockMetaProv.Get(split[2]);

                    if ( stockMeta != null)
                    {
                        sb.AppendLine($"CompanyName: {stockMeta.name}");
                    }
                    ReadOnlyCollection<SAlarm> alarms = _clientStalker.StockAlarms(split[2]);

                    foreach (SAlarm alarm in alarms)
                    {
                        sb.AppendLine($"Alarm: {alarm.AlarmType} lvl={alarm.Level} note={alarm.Note}");
                    }

                    string[] sectors = _clientStalker.GetStockSectors(split[2]);

                    for (int s = 0; s < SSector.MaxSectors; s++)
                    {
                        (string sectorName, _) = _clientStalker.GetSector(s);

                        if (sectors[s] != null)
                            sb.AppendLine($"{sectorName}: {sectors[s]}");
                    }

                    return new OkResult<string>(sb.ToString());
                }

            case "destroystock": //  sref
                {
                    StockMeta stockMeta = _stockMetaProv.Get(split[2]);

                    if (stockMeta == null)
                        return new FailResult<string>("Could not find stock");

                    sb.AppendLine($"*** {stockMeta.name} removing ... everything...");

                    // 1) All off from Stalker

                    _clientStalker.DoAction($"DeleteAll-Stock SRef=[{split[2]}]");

                    // 2) All off from StockMeta & MetaHistory

                    _stockMetaUpdate.DestroyStock(split[2]);

                    // delete fetch rules? nah not worth of trouble

                    // delete also EOD ?? later if needed.. as it cleans itself also on month or so...

                    return new OkResult<string>(sb.ToString());
                }

            case "sectors":
                {
                    for (int s = 0; s < SSector.MaxSectors; s++)
                    {
                        (string sectorName, string[] fieldNames) = _clientStalker.GetSector(s);

                        if (string.IsNullOrWhiteSpace(sectorName))
                            continue;

                        sb.AppendLine($"*** {sectorName}");

                        for ( int f = 0; f < SSector.MaxFields; f++)
                            if (fieldNames[f] != null )
                                sb.AppendLine($" {fieldNames[f]}");
                    }

                    return new OkResult<string>(sb.ToString());
                }

            case "partialbackupzip": // partialbackupzip symbols filename
                {
                    string[] symbols = split[2].Split(',');

                    byte[] zip = _clientData.ExportPartialBackupAsZip(symbols.ToList());

                    return new OkResult<string>(Convert.ToBase64String(zip));
                }

            case "storagedebugzip":
                {
                    byte[] zip = _clientData.ExportStorageDumpAsZip(string.Empty);
                    return new OkResult<string>(Convert.ToBase64String(zip));
                }
        }
        return new FailResult<string>($"Unknown command!");
    }

    public async Task<Result<string>> ClientHelpMeAsync(string cmd)
    {
        var parseResp = CmdParser.Parse(cmd, _cmdTemplates);

        if (parseResp.Fail) // parser gives per templates a proper fail w help
            return new FailResult<string>((parseResp as FailResult<Dictionary<string, string>>).Message);

        return new OkResult<string>("Cmd is OK:" + string.Join(",", parseResp.Data.Select(x => x.Key + "=" + x.Value)));
    }
}
