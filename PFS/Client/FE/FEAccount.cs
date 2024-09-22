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

using Pfs.Config;
using Pfs.ExtFetch;
using Pfs.Data;
using Pfs.Types;
using System.Collections.ObjectModel;
using static Pfs.Client.IFEAccount;
using static Pfs.Data.UserEvent;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Diagnostics.SymbolStore;

namespace Pfs.Client;

public class FEAccount : IFEAccount
{
    protected IPfsPlatform _pfsPlatform;
    protected IPfsStatus _pfsStatus;
    protected ClientData _clientData;
    protected ClientStalker _clientStalker;
    protected IMarketMeta _marketMetaProv;
    protected IStockMeta _stockMetaProv;
    protected ILatestEod _latestEodProv;
    protected IFetchEod _fetchEod;
    protected IFetchRates _fetchRates;
    protected ILatestRates _latestRatesProv;
    protected StoreUserEvents _storeUserEvents;
    protected IPfsSetMarketConfig _marketConfig;
    protected IStockNotes _stockNotes;
    protected IFEConfig _fEConfig;
    protected ClientReportPreCalcs _clientReportPreCalcs;

    public FEAccount(IPfsPlatform pfsPlatform, IPfsStatus pfsStatus, ClientData clientData, ClientStalker clientStalker, IMarketMeta marketMetaProv, IStockMeta stockMetaProv, ILatestEod latestEodProv, 
                     IFetchEod fetchEod, IFetchRates fetchRates, ILatestRates latestRatesProv, StoreUserEvents storeUserEvents, IPfsSetMarketConfig marketConfig,
                     IStockNotes stockNotes, IFEConfig fEConfig, ClientReportPreCalcs clientReportPreCalcs)
    {
        _pfsPlatform = pfsPlatform;
        _pfsStatus = pfsStatus;
        _clientData = clientData;
        _clientStalker = clientStalker;
        _marketMetaProv = marketMetaProv;
        _stockMetaProv = stockMetaProv;
        _latestEodProv = latestEodProv;
        _fetchEod = fetchEod;
        _fetchRates = fetchRates;
        _latestRatesProv = latestRatesProv;
        _storeUserEvents = storeUserEvents;
        _marketConfig = marketConfig;
        _stockNotes = stockNotes;
        _fEConfig = fEConfig;
        _clientReportPreCalcs = clientReportPreCalcs;
    }

    public AccountTypeId AccountType { get { return _pfsStatus.AccountType; } }

    public int GetAppCfg(AppCfgId id)
    {
        return _pfsStatus.GetAppCfg(id);
    }

    public List<MenuEntry> GetMenuData()
    {
        List<MenuEntry> ret = new List<MenuEntry>();

        ret.Add(new MenuEntry()
        {
            Name = "Home",
            Type = MenuEntryType.Home,
            ParentName = "",
            Path = "/",
        });

        List<SPortfolio> portfolios = _clientStalker.Portfolios();

        foreach (SPortfolio pf in portfolios)
        {
            ret.Add(new MenuEntry()
            {
                Name = pf.Name,
                Type = MenuEntryType.Portfolio,
                ParentName = "Home",
                Path = "/Home/",
            });
        }
        return ret;
    }

    public void SaveData()
    {
        _clientData.DoSaveData();
    }

    public void ClearLocally()
    {
        // Nukes EVERYTHING
        _pfsPlatform.PermClearAll();

        _clientData.DoInitDataOwners();
    }

    public Result LoadDemo(byte[] zip)
    {
        if (_pfsStatus.AccountType != AccountTypeId.Offline)
            return new FailResult("Cant load on this state");

        _pfsStatus.AllowUseStorage = false;
        _clientReportPreCalcs.InitClean();

        Result res = _clientData.ImportFromBackupZip(zip);

        if ( res.Ok )
            _pfsStatus.AccountType = AccountTypeId.Demo;

        return res;
    }

    public IEnumerable<MarketMeta> GetActiveMarketsMeta()
    {
        return _marketMetaProv.GetActives();
    }

    public MarketMeta GetMarketMeta(MarketId marketId)
    {
        return _marketMetaProv.Get(marketId);
    }

    public MarketStatus[] GetMarketStatus()
    {
        return _marketMetaProv.GetMarketStatus();
    }

    public UserEventAmounts GetUserEventAmounts()
    {
        return _storeUserEvents.GetAmounts();
    }

    public Result RefetchLatestRates()
    {
        return _fetchRates.FetchLatest(_fEConfig.HomeCurrency);
    }

    public (DateOnly date, CurrencyRate[] rates) GetLatestRatesInfo()
    {
        return _latestRatesProv.GetLatestInfo();
    }

    public async Task<decimal?> GetHistoryRateAsync(CurrencyId fromCurrencyId, DateOnly date)
    {
        return await _fetchRates.GetRateAsync(_latestRatesProv.HomeCurrency, fromCurrencyId, date);
    }

    public StockExpiredStatus GetExpiredEodStatus()
    {
        (int totalStocks, List<ExpiredStocks.Expired> expired) = ExpiredStocks.GetExpiredEods(_pfsPlatform.GetCurrentUtcTime(), _stockMetaProv, _latestEodProv, _marketMetaProv);

        int ndStocks = expired.Where(e => e.EodLocalDate == null).Count();

        return new StockExpiredStatus(totalStocks, expired.Count() - ndStocks, ndStocks);
    }

    public FetchProgress GetFetchProgress()
    {
        return _fetchEod.GetFetchProgress();
    }

    public Dictionary<MarketId, List<string>> GetExpiredStocks()
    {
        Dictionary<MarketId, List<string>> ret = new();

        (int _, List<ExpiredStocks.Expired> expired) = ExpiredStocks.GetExpiredEods(_pfsPlatform.GetCurrentUtcTime(), _stockMetaProv, _latestEodProv, _marketMetaProv);

        foreach (ExpiredStocks.Expired ex in expired)
        {
            (MarketId marketId, string symbol) = StockMeta.ParseSRef(ex.SRef);

            if (ret.ContainsKey(marketId) == false)
                ret.Add(marketId, new());

            ret[marketId].Add(symbol);
        }
        return ret;
    }

    public (int fetchAmount, int pendingAmount) FetchExpiredStocks()
    {
        (int _, List<ExpiredStocks.Expired> expired) = ExpiredStocks.GetExpiredEods(_pfsPlatform.GetCurrentUtcTime(), _stockMetaProv, _latestEodProv, _marketMetaProv);

        int pendingAmount = 0;
        Dictionary<MarketId, List<string>> fetch = new();

        foreach (ExpiredStocks.Expired ex in expired)
        {
            if (ex.GetState() == ExpiredStocks.Expired.State.Pending)
            {
                pendingAmount++;
                continue; // <== this here makes to wait markets 'MinFetchMins' before fetching
            }

            (MarketId marketId, string symbol) = StockMeta.ParseSRef(ex.SRef);

            if (fetch.ContainsKey(marketId) == false)
                fetch.Add(marketId, new());

            // Missing datas always or if not on 'pending after close period' then expired also
            fetch[marketId].Add(symbol);
        }

        if (fetch.Count == 0)
            return (0, pendingAmount);

        _pfsStatus.SendPfsClientEvent(PfsClientEventId.FetchEodsStarted);

        _fetchEod.Fetch(fetch);
        return (fetch.Values.Sum(list => list.Count), pendingAmount);
    }

    public void FetchStock(MarketId marketId, string symbol)
    {
        FetchProgress progr = _fetchEod.GetFetchProgress();

        if (progr.IsBusy())
            return;

        _pfsStatus.SendPfsClientEvent(PfsClientEventId.FetchEodsStarted);

        Dictionary<MarketId, List<string>> fetch = new();
        fetch.Add(marketId, new List<string>() { symbol });
        _fetchEod.Fetch(fetch);
    }

    public void ForceFetchToProvider(ExtProviderId provider, Dictionary<MarketId, List<string>> stocks)
    {
        _pfsStatus.SendPfsClientEvent(PfsClientEventId.FetchEodsStarted);
        _fetchEod.Fetch(stocks, provider);
    }

    public async Task<Dictionary<ExtProviderId, Result<FullEOD>>> TestStockFetchingAsync(MarketId marketId, string symbol, ExtProviderId[] providers)
    {
        return await _fetchEod.TestStockFetchingAsync(marketId, symbol, providers);
    }

    public void UpdateUserEventStatus(int id, UserEventStatus status)
    {
        _storeUserEvents.UpdateUserEventStatus(id, status);
    }

    public void DeleteUserEvent(int id)
    {
        _storeUserEvents.DeleteUserEvent(id);
    }

    public List<RepDataUserEvents> GetUserEventsData()
    {
        List<RepDataUserEvents> ret = new();

        ReadOnlyCollection<StoreUserEvents.UserEventInfo> events = _storeUserEvents.GetAll();

        foreach (StoreUserEvents.UserEventInfo ev in events)
        {
            Dictionary<EvFieldId, object> prms = ev.Data.GetFields();
            StockMeta sm = _stockMetaProv.Get((string)prms[EvFieldId.SRef]);

            if (sm == null) // We do wanna have this, or would need to delete automatic
                sm = _stockMetaProv.AddUnknown((string)prms[EvFieldId.SRef]);

            RepDataUserEvents entry = new RepDataUserEvents()
            {
                Date = (DateOnly)prms[EvFieldId.Date],
                Type = (UserEventType)prms[EvFieldId.Type],
                Status = ev.Status,
                Id = ev.Id,
                StockMeta = sm,
            };

            if (prms.ContainsKey(EvFieldId.Portfolio))
                entry.PfName = (string)prms[EvFieldId.Portfolio];

            switch (entry.Type)
            {
                case UserEventType.OrderBuy:
                case UserEventType.OrderSell:
                case UserEventType.OrderBuyExpired:
                case UserEventType.OrderSellExpired:
                    entry.Order = new()
                    {
                        PricePerUnit = (decimal)prms[EvFieldId.Value],
                        Units = (decimal)prms[EvFieldId.Units],
                    };

                    if ( entry.Type == UserEventType.OrderBuyExpired || entry.Type == UserEventType.OrderBuy)
                        entry.Order.Type = SOrder.OrderType.Buy;
                    else
                        entry.Order.Type = SOrder.OrderType.Sell;
                    break;

                case UserEventType.AlarmOver:
                case UserEventType.AlarmUnder:
                    entry.Alarm = new()
                    {
                        AlarmValue = (decimal)prms[EvFieldId.Value],
                        DayClosed = (decimal)prms[EvFieldId.EodClose],
                    };

                    if (prms.ContainsKey(EvFieldId.EodLow))
                        entry.Alarm.DayLow = (decimal)prms[EvFieldId.EodLow];

                    if (prms.ContainsKey(EvFieldId.EodHigh))
                        entry.Alarm.DayHigh = (decimal)prms[EvFieldId.EodHigh];

                    break;
            }
            ret.Add(entry);
        }
        return ret;
    }

    public Note GetNote(string sRef)
    {
        return _stockNotes.Get(sRef);
    }

    public void StoreNote(string sRef, Note note)
    {
        _stockNotes.Store(sRef, note);
    }

    public byte[] ExportAccountBackupAsZip()
    {
        return _clientData.ExportAccountBackupAsZip();
    }

    public Result ImportAccountFromZip(byte[] zip)
    {
        // This is expected to be done here, as Import atm is only tested to clean account (merging is NOT supported)
        ClearLocally();

        return _clientData.ImportFromBackupZip(zip);
    }
}
