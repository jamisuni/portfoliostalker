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

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Pfs.Config;
using Pfs.ExtProviders;
using Pfs.Helpers;
using Pfs.Types;
using Serilog;

namespace Pfs.ExtFetch;

public class FetchEod : IFetchEod, ICmdHandler, IOnUpdate, IDataOwner
{
    /* Main component that ties everything together, and implements public interfaces
     * - Simple do fetch interface that takes market and symbols to be fetched
     * - Follows 'IPfsGetProvConfig' to know what providers are available for fetching
     * - Uses 'IPfsUseFetchConfig' to decide what providers are used for specific symbols
     * - Creates and owns ExtProvider instances itself so they dont use DI
     * - Each ExtProvider spins own thread from pool, and writes res/err on dedicated spot
     * - Registers as 'scheduler client' to receive regular/timed OnUpdate events
     * - All main processing is done under single thread, just ExtProviders are on own threads
     * -> No storing of EOD but just sends event w new data
     */

    protected const string _componentName = "fetcheod";

    protected readonly IPfsPlatform _pfsPlatform;
    protected readonly IPfsStatus _pfsStatus;
    protected readonly IPfsProvConfig _provConfig;
    protected readonly IMarketMeta _marketMetaProv;
    private readonly FetchEodPending _pendingSymbols;
    protected ExtProviderId _newFetchEnforceProvider = ExtProviderId.Unknown;
    protected ConcurrentQueue<(MarketId market, string symbols)> _newFetch = new(); // <= recv reqs
    private FetchEodTask[] _fetchTask;
    protected Dictionary<MarketId, DateOnly> _latestCloseDate = null;

    // Rotating 'latest' records array w 'max' spots, and 'OldestPos' marking next spot to use (just for visual purposes for 'latest' command)
    protected record LatestInfo(bool Result, DateTime FetchLocalTime, ExtProviderId Provider, MarketId Market, string Symbol, DateOnly? EodDate, decimal? EodClose);
    protected const int _latestMax = 30;
    protected int _latestOldestPos = 0; // goes backward, and new one added this spot
    protected LatestInfo[] _latest = new LatestInfo[_latestMax]; // pos+1 is newest

    protected FetchProgress _fetchProgress = new();

    // Lists all 'failed' fetchings over all providers, including errorMsg from provider
    protected record FailedInfo(int fetchId, ExtProviderId Provider, string ErrorMsg);
    protected Dictionary<string, List<FailedInfo>> _failed = new(); // all uptime, so full failure history is available
    protected int _fetchId = 0; // counter from 1... to track fetch attempts

    protected readonly static ImmutableArray<string> _cmdTemplates = [
        "list",
        "fetch <market> symbols",
        "status",
        "latest",                   // ok/not for all providers shows last 30 fetch results
        "failed",
        "resetcredits"
    ];

    // !!!THINK!!! There is big open issue w restore backups what should happen here.. but not worth of over stressing atm.. as reload fixes all issues

    public FetchEod(IPfsPlatform pfsPlatform, IPfsStatus pfsStatus, IPfsFetchConfig fetchConfig, IPfsProvConfig provConfig, IMarketMeta marketMetaProv)
    {
        _pfsPlatform = pfsPlatform;
        _pfsStatus = pfsStatus;
        _provConfig = provConfig;
        _marketMetaProv = marketMetaProv;

        _pendingSymbols = new FetchEodPending(fetchConfig);

        _provConfig.EventProvConfigsChanged += OnEventProvConfigsChanged;
    }

    protected void InitProviders()
    {
        List<FetchEodTask> tasks = new();

        Dictionary<ExtProviderId, FetchEodTask.ProvPermInfo> permTaskInfo = LoadStorageContent();

        {
            ExtAlphaVantage alpha = new(_pfsStatus);
            FetchEodTask alphaF = new(_pfsStatus, ExtProviderId.AlphaVantage, alpha, alpha, permTaskInfo.TryGetValue(ExtProviderId.AlphaVantage, out var perm_a) ? perm_a : null);
            alphaF.SetPrivKey(_provConfig.GetPrivateKey(ExtProviderId.AlphaVantage));
            tasks.Add(alphaF);
        }
        {
            ExtPolygon polygon = new(_pfsStatus);
            FetchEodTask polygonF = new(_pfsStatus, ExtProviderId.Polygon, polygon, polygon, permTaskInfo.TryGetValue(ExtProviderId.Polygon, out var perm_p) ? perm_p : null);
            polygonF.SetPrivKey(_provConfig.GetPrivateKey(ExtProviderId.Polygon));
            tasks.Add(polygonF);
        }
        {
            ExtTwelveData twelve = new(_pfsStatus);
            FetchEodTask twelveF = new(_pfsStatus, ExtProviderId.TwelveData, twelve, twelve, permTaskInfo.TryGetValue(ExtProviderId.TwelveData, out var perm_t) ? perm_t : null);
            twelveF.SetPrivKey(_provConfig.GetPrivateKey(ExtProviderId.TwelveData));
            tasks.Add(twelveF);
        }
        {
            ExtUnibit unibit = new();
            FetchEodTask unibitF = new(_pfsStatus, ExtProviderId.Unibit, unibit, unibit, permTaskInfo.TryGetValue(ExtProviderId.Unibit, out var perm_u) ? perm_u : null);
            unibitF.SetPrivKey(_provConfig.GetPrivateKey(ExtProviderId.Unibit));
            tasks.Add(unibitF);
        }
        {
            ExtMarketstack marketstack = new(_pfsStatus);
            FetchEodTask marketstackF = new(_pfsStatus, ExtProviderId.Marketstack, marketstack, marketstack, permTaskInfo.TryGetValue(ExtProviderId.Marketstack, out var perm_m) ? perm_m : null);
            marketstackF.SetPrivKey(_provConfig.GetPrivateKey(ExtProviderId.Marketstack));
            tasks.Add(marketstackF);
        }
        {
            ExtFmp fmp = new(_pfsStatus);
            FetchEodTask fmpF = new(_pfsStatus, ExtProviderId.FMP, fmp, fmp, permTaskInfo.TryGetValue(ExtProviderId.FMP, out var perm_i) ? perm_i : null);
            fmpF.SetPrivKey(_provConfig.GetPrivateKey(ExtProviderId.FMP));
            tasks.Add(fmpF);
        }
        //{ pending - cors issues, looks like their api doesnt work directly from browser 
        //  ExtMarketDataTiingo tiingo = new(pfsStatus);
        //  FetchEodTask tiingoF = new(pfsStatus, ExtProviderId.Tiingo, tiingo, tiingo, permTaskInfo.TryGetValue(ExtProviderId.Tiingo, out var perm_t) ? perm_t : null);
        //  tiingoF.SetPrivKey(_provConfig.GetPrivateKey(ExtProviderId.Tiingo));
        //  tasks.Add(tiingoF);
        //}
        _fetchTask = tasks.ToArray();

        return;
    }

    protected void OnEventProvConfigsChanged(object obj, ExtProviderId provId)
    {   // Shouldnt matter what thread this is called as just sets key to be used on future 
        FetchEodTask ft = _fetchTask.SingleOrDefault(f => f.ProvId == provId);

        if (ft == null)
            return;

        ft.SetPrivKey(_provConfig.GetPrivateKey(provId));
    }

    protected void TriggerEventReceivedEod(MarketId market, string symbol, FullEOD[] data)
    {
        if (market == MarketId.LSE) // Note! So far all providers return LSE/London as pennies, so needs /100 to get pounds
            foreach (FullEOD d in data)
                d.DivideBy(100);

        _pfsStatus.SendPfsClientEvent(PfsClientEventId.ReceivedEod,
            new ReceivedEodArgs(market, symbol, data));
        return;
    }

    public void Fetch(Dictionary<MarketId, List<string>> symbols, ExtProviderId provider = ExtProviderId.Unknown) // IFetchEod - could be wrong task so just queue
    {
        if (_pendingSymbols.TotalPending() > 0)
            return; // silent failure, as not supposed to happen with UI

        // Each new fetch request, is clearning some counters to see what happens w this fetch

        _pendingSymbols.ClearAllPendings();
        _pendingSymbols.ClearFailed();
        _fetchId++; //_failed = new(); no clean, as want to have it full uptime but it has mark for id to know older fetches

        foreach (FetchEodTask ft in _fetchTask)
        {
            // Informative counters per provider telling success/failure for this fetch
            ft.ResetFetchCounters();    
            // Actual credit counters for provider, if day/month changed gets new credits
            ft.CheckCreditCounters();
        }

        _fetchProgress = new()
        {
            Requested = symbols.Sum(d => d.Value.Count),
        };

        // Note! Fixing expected date per fetch time, so anything older than this is rejected
        Local_UpdLastCloseDateForMarkets();

        // Most time this is unknown, but tracking has force button that allows to enforce all fetch to specific provider
        _newFetchEnforceProvider = provider;

        foreach ( KeyValuePair<MarketId, List<string>> kvp in symbols)
            // Goes to queue as dont wanna risk thread issues more than this -> OnUpdateAsync
            _newFetch.Enqueue((kvp.Key, string.Join(',', kvp.Value)));

        return;

        void Local_UpdLastCloseDateForMarkets()
        {   // allows to know per market what date EOD should be to be latest
            _latestCloseDate = new();
            foreach (MarketId marketId in Enum.GetValues(typeof(MarketId)))
            {
                if (marketId.IsReal() == false)
                    continue;

                (DateOnly localDate, _) = _marketMetaProv.LastClosing(marketId);

                _latestCloseDate.Add(marketId, localDate);
            }
        }
    }

    public async Task<Dictionary<ExtProviderId, Result<FullEOD>>> TestStockFetchingAsync(MarketId marketId, string symbol, ExtProviderId[] providers)      // IFetchEod
    {   // Called when user wants manually test some symbol w specific providers, ala instant "fetch it now and show me result fetch"
        ConcurrentDictionary<ExtProviderId, Result<FullEOD>> result = new();
        List<Task> paraller = new();

        foreach ( ExtProviderId provId in providers)
        {
            result.TryAdd(provId, null);

            paraller.Add(Task.Run(async () => result[provId] = await _fetchTask.Single(f => f.ProvId == provId).TestStockFetchingAsync(marketId, symbol)));
        }

        await Task.WhenAll(paraller);

        return result.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    public async Task<StockMeta[]> FindBySymbolAsync(string symbol, MarketId optMarketId = MarketId.Unknown, CurrencyId optMarketCurrency = CurrencyId.Unknown)
    {   // As of 2024-Mar, doesnt require key so lets go lazy way now without task implementation
        // This is specific functionality we use from TwelveData... as they seam have nice API for matching
        StockMeta[] found;
        if ( optMarketId == MarketId.Unknown )
            found = await new ExtTwelveData(_pfsStatus).FindBySymbolAsync(symbol, [optMarketId]);
        else
            found = await new ExtTwelveData(_pfsStatus).FindBySymbolAsync(symbol, _marketMetaProv.GetActives().Select(m => m.ID).ToArray());

        if ( found != null && found.Count() > 1 && optMarketId == MarketId.Unknown && optMarketCurrency != CurrencyId.Unknown )
        {   // As market was not defined, and we use actives.. got multiple matches... use currency to close decision
            IEnumerable<MarketMeta> actives = _marketMetaProv.GetActives();
            // Note! 'found' contains partially filled StockMeta wo currency been set, thats why need 'actives'
            found = found.Where(f => actives.Single(m => m.ID == f.marketId).Currency == optMarketCurrency).ToArray();
        }
        return found;
    }

    public IEnumerable<ExtProviderId> GetActiveEodProviders(MarketId marketId)
    {
        return _fetchTask.Where(f => f.IsActive(marketId)).Select(f => f.ProvId).ToList();
    }

    // This is called from UI to every few seconds to update fetch dialog that shows progress
    public FetchProgress GetFetchProgress()
    {
        _fetchProgress.TotalLeft = _pendingSymbols.TotalPending();
        _fetchProgress.PriorityLeft = _pendingSymbols.TotalPriorityPending();
        _fetchProgress.Failed = 0;
        _fetchProgress.Succeeded = 0;
        List<FetchProgress.PerProv> provInfo = new();

        foreach (FetchEodTask ft in _fetchTask) // Get status of each provider to ret
        {
            if (ft.GetState() == FetchEodTask.State.Disabled)
                continue;

            Dictionary<MarketId, (int fail, int success)> ftInfo = ft.GetFetchInfo();

            FetchProgress.PerProv info = new()
            {
                ProvId = ft.ProvId,
                Busy = ft.GetState() != FetchEodTask.State.Free,
                Failed = ftInfo.Values.Sum(f => f.fail),
                Success= ftInfo.Values.Sum(f => f.success),
                CreditsLeft = ft.DailyCreditsLeft(),
            };
            _fetchProgress.Succeeded += info.Success;
            _fetchProgress.Failed += info.Failed;
            provInfo.Add(info);
        }
        _fetchProgress.ProvInfo = provInfo.ToArray();
        _fetchProgress.Ignored = _pendingSymbols.GetCantFindProviderSRefs().Count;

        return _fetchProgress;
    }

    public async Task OnUpdateAsync(DateTime timestamp)                             // IOnUpdate - this is our 'safe' thread to do all main processing!
    {
        if (_newFetch.IsEmpty == false)
        {   // All new requests are processed here, as we do not wanna risk thread issues
            if (_newFetch.TryDequeue(out (MarketId marketId, string symbols) result))
                _pendingSymbols.AddToPending(result.marketId, result.symbols, _newFetchEnforceProvider);
        }

        bool finishedAnything = false;

        foreach (FetchEodTask task in _fetchTask)
        {   // Start 'FetchTask' checking by looking all 'Ready/Error' ones -> process results and release them
            if (task.GetState() == FetchEodTask.State.Ready)
            {
                Local_ProcessFetchReady(task);
                finishedAnything = true;
            }
            else if (task.GetState() == FetchEodTask.State.Error)
            {
                Local_HandleFetchError(task);
                finishedAnything = true;
            }
        }

        if (_pendingSymbols.TotalPending() == 0)
        {
            if (finishedAnything)
            {
                // So we did finish up some fetching, and dont have more => send global event that "fetch is resting now"
                await _pfsStatus.SendPfsClientEvent(PfsClientEventId.FetchEodsFinished, null);

                // As credits are more critical to keep correct counting, we enforcing here local saving of that info on end
                // teoretically most cases these gets stored as EOD itself causes state to be pending to save assuming user
                // actually saved.. but on case where fetch fails and none success wouldnt even propose saving even used credits.
                BackupToStorage();
            }
            return;
        }

        bool anyoneNotFree = false;

        // Is anyone free... 
        foreach (FetchEodTask task in _fetchTask)
        {
            if (task.GetState() == FetchEodTask.State.Disabled)
                continue;

            if (task.GetState() != FetchEodTask.State.Free)
            {
                anyoneNotFree = true;
                continue;
            }

            MarketId[] provMarkets = task.GetMarkets(); // if out of credits, then no markets

            if (provMarkets.Count() == 0)
                continue;

            // See if has anything available for it
            var fetch = _pendingSymbols.GetPending(task.ProvId, 1, provMarkets);        // !!!TODO!!! Activate BATCH later!

            if (fetch.symbols == null)
                continue;

            anyoneNotFree = true;

            // Launches fetching, that response is later available when state gets updated
            task.LaunchFetchEodLatest(fetch.market, fetch.symbols, _latestCloseDate[fetch.market]);
        }

        if ( anyoneNotFree == false)
        {   // So has pendings, no one is busy, and nothing happens => rest missing rules?
            _pendingSymbols.SetRestToFailedSRefs();

            await _pfsStatus.SendPfsClientEvent(PfsClientEventId.FetchEodsFinished, null);
        }
        return;

        void Local_ProcessFetchReady(FetchEodTask task)
        {
            var okResp = task.GetOkResult();

            foreach ( KeyValuePair<string, FullEOD> kvp in okResp.eod)
            {
                Local_AddLatest(new(true, DateTime.Now, task.ProvId, okResp.marketId, kvp.Key, kvp.Value.Date, kvp.Value.Close));

                TriggerEventReceivedEod(okResp.marketId, kvp.Key, [kvp.Value]);
            }
        }

        void Local_HandleFetchError(FetchEodTask task)
        {
            var errorResp = task.GetErrorResult();

            if ( errorResp.symbols.Count > 1)
            {
                // !!!TODO!!! Batch failure so lock provider off from batching (actually do this on Task side, but not yet)
            }

            // Each failed fetch get tracked on full uptime, and if fails many providers then each and everyone is tracked
            // point is to be able to see details of fetch issues all the way on UI dialog
            foreach (string symbol in errorResp.symbols)
            {
                string sRef = $"{errorResp.marketId}${symbol}";

                if (_failed.ContainsKey(sRef) == false)
                    _failed.Add(sRef, new());

                _failed[sRef].Add(new FailedInfo(_fetchId, task.ProvId, errorResp.errMsg));

                // Later! Atm also pending side tracks failures (to not allow retry w same provider), but can replace this w callback etc
                _pendingSymbols.FetchFailedBy(task.ProvId, errorResp.marketId, symbol);
            }

            foreach (string symbol in errorResp.symbols)
                Local_AddLatest(new(false, DateTime.Now, task.ProvId, errorResp.marketId, symbol, null, null));
        }

        void Local_AddLatest(LatestInfo info)
        {
            _latest[_latestOldestPos--] = info;
            if ( _latestOldestPos < 0)
                _latestOldestPos = _latestMax - 1;
        }
    }

    public string GetCmdPrefixes() { return _componentName; }

    public async Task<Result<string>> CmdAsync(string cmd)                  // ICmdHandler
    {
        await Task.CompletedTask;

        MarketId marketId;
        StringBuilder sb = new();
        var parseResp = CmdParser.Parse(cmd, _cmdTemplates);

        if (parseResp.Fail) // parser gives per templates a proper fail w help
            return new FailResult<string>((parseResp as FailResult<Dictionary<string, string>>).Message);

        switch (parseResp.Data["cmd"])
        {
            case "list":
                return new OkResult<string>("todo ProvEod");

            case "fetch": // "fetch <market> symbols"
                {
                    var temp = new Dictionary<MarketId, List<string>>(); 
                    marketId = Enum.Parse<MarketId>(parseResp.Data["<market>"]);
                    temp.Add(marketId, parseResp.Data["symbols"].Split(',').ToList());
                    Fetch(temp);
                    return new OkResult<string>($"Added to fetch queue!");
                }

#if false // lot of complex code for never be used... to be removed...
            case "reset":
                {
                    sb.AppendLine($"Clearing {_failed.Count} entries from Failed -list");
                    _failed = new();

                    _pendingSymbols.ClearFailed();

                    if (_pendingSymbols.TotalPending() > 0)
                    {
                        sb.AppendLine($"Clearing {_pendingSymbols.TotalPending()} entries from Pending -list");
                        _pendingSymbols.ClearAllPendings();
                    }
                    else
                        sb.AppendLine($"Nothing on pending list to clear up.");

                    sb.AppendLine($"Clearing {_latest.Count(item => item != null)} items from Latest -list");

                    foreach (var task in _fetchTask)
                    {
                        task.ResetFetchCounters();

                        if (task.GetState() != FetchEodTask.State.Disabled && task.GetState() != FetchEodTask.State.Free)
                            continue;

                        sb.AppendLine($"Task {task.ProvId} is busy on state {task.GetState()}, and cant be stopped");
                    }
                    return new OkResult<string>(sb.ToString());
                }
#endif

            case "resetcredits":
                {   // Keeper - if something goes wrong counters then allows to do full reset mid month
                    foreach (var task in _fetchTask)
                        task.ResetCreditCounters();

                    EventNewUnsavedContent?.Invoke(this, _componentName);
                    return new OkResult<string>("Values resetted, use *status* to see new limits!");
                }

            case "status":
                {
                    sb.AppendLine($"Pending on priority queue: {_pendingSymbols.TotalPriorityPending()}");
                    sb.AppendLine($"Pending on market queue: {string.Join(", ", _pendingSymbols.GetMarketPendingStats().Select(kv => $"{kv.Key}={kv.Value}"))}");
                    foreach (var task in _fetchTask)
                        sb.AppendLine($" {task.ProvId} state={task.GetStatusInfo()}");
                    return new OkResult<string>(sb.ToString());
                }

            case "latest":
                {
                    sb.AppendLine($"Latest Fetch Results:");
                    int p = _latestOldestPos;
                    for (int done = 0; done < _latestMax; done++)
                    {
                        if (++p >= _latestMax)
                            p = 0;

                        if (_latest[p] == null)
                            continue;

                        LatestInfo L = _latest[p];

                        if ( L.Result == true )
                            sb.AppendLine($"{L.FetchLocalTime.ToString("HH:mm")} {L.Market}${L.Symbol} by {L.Provider} at {L.EodDate.Value.ToYMD()} was {L.EodClose.Value.ToString("0.00")}");
                        else
                            sb.AppendLine($"{L.FetchLocalTime.ToString("HH:mm")} {L.Market}${L.Symbol} by {L.Provider} failed!");
                    }
                    return new OkResult<string>(sb.ToString());
                }

            case "failed": // This is list also fetch dialog shows when wanting to see details of failures
                {
                    sb.AppendLine($"All Failed Operations:");

                    foreach ( KeyValuePair<string, List<FailedInfo>> kvp in _failed)
                    {
                        sb.AppendLine($"{kvp.Key} failed:");
                        foreach (FailedInfo fail in kvp.Value)
                        {
                            string fetch = fail.fetchId == _fetchId ? "(latest)" : $"({fail.fetchId})";
                            sb.AppendLine($"     {fetch} by {fail.Provider} failed: {fail.ErrorMsg}");
                        }
                    }

                    if (_pendingSymbols.GetCantFindProviderSRefs().Count() > 0)
                    {
                        sb.AppendLine($"Failed to find provider: {string.Join(',', _pendingSymbols.GetCantFindProviderSRefs())}");
                    }
                    return new OkResult<string>(sb.ToString());
                }
        }

        throw new NotImplementedException($"ProvEod.CmdAsync missing {parseResp.Data["cmd"]}");
    }

    public async Task<Result<string>> HelpMeAsync(string cmd)                                   // ICmdHandler
    {
        await Task.CompletedTask;

        var parseResp = CmdParser.Parse(cmd, _cmdTemplates);

        if (parseResp.Fail) // parser gives per templates a proper fail w help
            return new FailResult<string>((parseResp as FailResult<Dictionary<string, string>>).Message);

        return new OkResult<string>("Cmd is OK:" + string.Join(",", parseResp.Data.Select(x => x.Key + "=" + x.Value)));
    }

    public event EventHandler<string> EventNewUnsavedContent; //<= doesnt send dirty event, but trusts that as data is received thats going to get saved together w credits                             
    public string GetComponentName() { return _componentName; }                                 // IDataOwner       -- dont really fit w credits data to model. needs thinking!
    public void OnInitDefaults() { /*Init();????*/ }
    public List<string> OnLoadStorage() { InitProviders(); return new(); } // here as cant be done before others are ready also
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
        // Its just credit counters atm on backup, so cant see importance of restoring them
        return new();
    }

    internal Dictionary<ExtProviderId, FetchEodTask.ProvPermInfo> LoadStorageContent()
    {
        try
        {
            string stored = _pfsPlatform.PermRead(_componentName);

            if (string.IsNullOrWhiteSpace(stored))
                return new();

            return JsonSerializer.Deserialize<Dictionary<ExtProviderId, FetchEodTask.ProvPermInfo>>(stored);        // !!!TODO!!! ImportXml
        }
        catch (Exception ex)
        {
            Log.Warning($"{_componentName} RestoreBackup failed to exception: [{ex.Message}]");
            _pfsPlatform.PermRemove(_componentName);
        }
        return new();
    }

    protected void BackupToStorage()
    {
        Dictionary<ExtProviderId, FetchEodTask.ProvPermInfo> store = new();

        foreach (var task in _fetchTask)
            if ( task.PermInfo != null )
                store.Add(task.ProvId, task.PermInfo);

        _pfsPlatform.PermWrite(_componentName, JsonSerializer.Serialize(store));                                    // !!!TODO!!! ExportXml
    }

    protected string ExportXml()
    {
        XElement rootPFS = new XElement("PFS");

        // Keep this separate as so much more critical information
        XElement allElem = new XElement("FetchEOD");
        rootPFS.Add(allElem);

        foreach (var ft in _fetchTask)
        {
            if (ft.PermInfo == null)
                continue;

            XElement ftElem = new XElement(ft.ProvId.ToString());
            ftElem.SetAttributeValue("Date", ft.PermInfo.CreditDateUtc.ToYMD());

            if ( ft.PermInfo.DaysCreditLeft.HasValue )
                ftElem.SetAttributeValue("Day", ft.PermInfo.DaysCreditLeft);

            if (ft.PermInfo.MonthsCreditLeft.HasValue)
                ftElem.SetAttributeValue("Month", ft.PermInfo.MonthsCreditLeft);

            allElem.Add(ftElem);
        }

        return rootPFS.ToString();
    }
#if false
    protected Dictionary<AppCfgId, int> ImportXml(string xml)
    {
        XDocument xmlDoc = XDocument.Parse(xml);
        XElement allUserCfgElem = xmlDoc.Element("AppCfg");

        Dictionary<AppCfgId, int> ret = new();

        XElement allMarketsElem = allUserCfgElem.Element("Markets");
        if (allMarketsElem != null && allMarketsElem.HasElements)
        {
            foreach (XElement mcElem in allMarketsElem.Elements())
            {
                AppCfgId id = (AppCfgId)Enum.Parse(typeof(AppCfgId), mcElem.Name.ToString());

                int value = (int)mcElem.Attribute("Value");

                ret.Add(id, value);
            }
        }
        return ret;
    }
#endif
}
