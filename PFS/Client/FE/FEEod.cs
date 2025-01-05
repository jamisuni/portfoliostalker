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
using static Pfs.Client.IFEEod;

namespace Pfs.Client;

public class FEEod : IFEEod
{
    protected IPfsPlatform _pfsPlatform;
    protected IPfsStatus _pfsStatus;
    protected ClientData _clientData;
    protected ClientStalker _clientStalker;
    protected IMarketMeta _marketMetaProv;
    protected IStockMeta _stockMetaProv;
    protected IEodLatest _latestEodProv;
    protected IFetchEod _fetchEod;
    protected IFetchRates _fetchRates;
    protected ILatestRates _latestRatesProv;
    protected StoreUserEvents _storeUserEvents;
    protected IPfsSetMarketConfig _marketConfig;
    protected IStockNotes _stockNotes;
    protected IFEConfig _fEConfig;
    protected ClientReportPreCalcs _clientReportPreCalcs;
    protected StoreLatestEod _storeLatestEod;

    public FEEod(IPfsPlatform pfsPlatform, IPfsStatus pfsStatus, ClientData clientData, ClientStalker clientStalker, IMarketMeta marketMetaProv, IStockMeta stockMetaProv, IEodLatest latestEodProv, 
                     IFetchEod fetchEod, IFetchRates fetchRates, ILatestRates latestRatesProv, StoreUserEvents storeUserEvents, IPfsSetMarketConfig marketConfig,
                     IStockNotes stockNotes, IFEConfig fEConfig, ClientReportPreCalcs clientReportPreCalcs, StoreLatestEod storeLatestEod)
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
        _storeLatestEod = storeLatestEod;
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

    public FullEOD GetLatestSavedEod(MarketId marketId, string symbol)
    {
        return _latestEodProv.GetFullEOD(marketId, symbol);
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

    public void AddEod(MarketId marketId, string symbol, FullEOD eod)
    {
        _storeLatestEod.Store(marketId, symbol, [eod]);

        _pfsStatus.SendPfsClientEvent(PfsClientEventId.StockUpdated, $"{marketId}${symbol}");
    }
}
