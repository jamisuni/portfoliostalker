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

using Pfs.ExtFetch;
using Pfs.Data.Stalker;
using Pfs.Types;
using System.Collections.ObjectModel;

namespace Pfs.Client;

public class FEStalker : IFEStalker
{
    protected ClientStalker _clientStalker;
    protected IMarketMeta _marketMetaProv;
    protected IStockMeta _stockMetaProv;
    protected IFetchEod _fetchEod;
    protected IStockMetaUpdate _stockMetaUpdate;
    protected IPfsStatus _pfsStatus;
    protected IPfsPlatform _pfsPlatform;

    public FEStalker(IPfsPlatform pfsPlatform, IPfsStatus pfsStatus, ClientStalker clientStalker, IMarketMeta marketMetaProv, IStockMeta stockMetaProv, IStockMetaUpdate stockMetaUpdate, IFetchEod fetchEod)
    {
        _pfsStatus = pfsStatus;
        _pfsPlatform = pfsPlatform;
        _clientStalker = clientStalker;
        _marketMetaProv = marketMetaProv;
        _stockMetaProv = stockMetaProv;
        _stockMetaUpdate = stockMetaUpdate;
        _fetchEod = fetchEod;
    }

    // This is mainly to be used on dialogs etc those wanna perform stalker data updates
    public Result DoAction(string cmd)
    {
        return _clientStalker.DoAction(cmd);
    }

    public StalkerDoCmd GetCopyOfStalker()
    {
        StalkerDoCmd copyContent = new();
        StalkerDoCmd.DeepCopy(_clientStalker, copyContent);

        return copyContent;
    }

    public Result DoActionSet(List<string> actionSet)
    {
        Result res = new OkResult();

        try
        {
            foreach (string action in actionSet)
            {
                res = _clientStalker.DoAction(action);

                if (res.Ok == false)
                    break;
            }
            return res;
        }
        catch (Exception ex)
        {
            return new FailResult($"Failed to exception: {ex.Message}");
        }
    }

    public StockMeta GetStockMeta(MarketId marketId, string symbol)
    {
        return _stockMetaProv.Get(marketId, symbol);
    }

    public StockMeta CloseStock(MarketId marketId, string symbol, DateOnly date, string comment)
    {
        // Lets verify that this even exists
        if (GetStockMeta(marketId, symbol) == null)
            return null;

        // 1) Update Stalker

        // Close-Stock SRef Date Note
        Result stalkerRes = _clientStalker.DoAction($"Close-Stock SRef=[{marketId}${symbol}] Date=[{date.ToYMD()}] Note=[{comment}]");

        if (stalkerRes.Ok == false)
            return null;

        // 2) Update StockMeta

        _stockMetaUpdate.CloseStock($"{marketId}${symbol}", date, comment);

        // Note! 'StoreLatestEod' is not cleaned as it has its own automatic cleaning for unused stocks

        // Later! 'StoreUserEvents' could be updated, but then not worth of effort atm

        return GetStockMeta(MarketId.CLOSED, symbol);
    }
    
    public Result SplitStock(MarketId marketId, string symbol, DateOnly date, decimal splitFactor, string comment)
    {
        // Lets verify that this even exists
        if (GetStockMeta(marketId, symbol) == null)
            return new FailResult("Didnt find stock");

        // 1) Update Stalker
        Result stalkerRes = _clientStalker.DoAction($"Split-Stock SRef=[{marketId}${symbol}] SplitFactor=[{splitFactor}]");
        if (stalkerRes.Ok == false)
            return stalkerRes;

        // 2) Update StockMeta
        _stockMetaUpdate.SplitStock($"{marketId}${symbol}", date, $"factor=[{splitFactor}] comment");

        // Later! 'StoreLatestEod' could well be tuned to match new split and actually add 'FindSplit' that looks EODs to calculate date+factor

        return new OkResult();
    }

    public StockMeta UpdateStockMeta(MarketId marketId, string symbol, MarketId updMarketId, string updSymbol, string updName, DateOnly date, string comment)
    {
        // Lets verify that this even exists
        if (GetStockMeta(marketId, symbol) == null)
            return null;

        // 1) Update Stalker

        Result stalkerRes = _clientStalker.DoAction($"Set-Stock UpdSRef=[{updMarketId}${updSymbol}] OldSRef=[{marketId}${symbol}]");

        if (stalkerRes.Ok == false)
            return null;

        // 2) Update StockMeta

        _stockMetaUpdate.UpdateFullMeta($"{updMarketId}${updSymbol}", $"{marketId}${symbol}", updName, date, comment);

        // Note! 'StoreLatestEod' is not cleaned as it has its own automatic cleaning for unused stocks

        // Later! 'StoreUserEvents' could be updated, but then not worth of effort atm

        return GetStockMeta(updMarketId, updSymbol);
    }

    public StockMeta AddNewStockMeta(MarketId marketId, string symbol, string companyName, string ISIN = "")
    {   // !!!TODO!!! Needs to add here verifications and return null if fails
        _stockMetaUpdate.AddStock(marketId, symbol, companyName, ISIN);

        StockMeta sm = _stockMetaProv.Get(marketId, symbol);

        if ( sm == null ) return null;

        _pfsStatus.SendPfsClientEvent(PfsClientEventId.StockAdded, sm.GetSRef());

        return sm;
    }

    public StockMeta UpdateCompanyNameIsin(MarketId marketId, string symbol, DateOnly date, string companyName, string ISIN = "")
    {
        StockMeta sm = _stockMetaProv.Get(marketId, symbol);

        if (sm == null) return null;

        if (string.IsNullOrWhiteSpace(ISIN) == false && sm.ISIN != ISIN && _stockMetaUpdate.UpdateIsin(marketId, symbol, date, ISIN) == false)
            return null;

        if (string.IsNullOrWhiteSpace(companyName) == false && sm.name != companyName && _stockMetaUpdate.UpdateCompanyName(marketId, symbol, date, companyName) == false)
            return null;

        return _stockMetaProv.Get(marketId, symbol);
    }

    public void AddSymbolSearchMapping(string fromSymbol, MarketId toMarketId, string toSymbol, string comment)
    {
        _stockMetaUpdate.AddSymbolSearchMapping(fromSymbol, toMarketId, toSymbol, comment);
    }

    // Match to single local stock per isin/symbol, with option to use currency as extra market limitor
    public StockMeta FindStock(string symbol, CurrencyId optMarketCurrency = CurrencyId.Unknown, string optISIN = null)
    {
        StockMeta sm;

        // 1) If broker etc give ISIN, and we happen to have it on meta then use that stock
        if ( string.IsNullOrWhiteSpace(optISIN) == false )
        {
            sm = _stockMetaProv.GetByISIN(optISIN);

            if ( sm != null)
                return sm;
        }

        if ( optMarketCurrency != CurrencyId.Unknown )
        {   // 2) As currency is defined, priority pick is market with that currency
            foreach (MarketMeta marketMeta in _marketMetaProv.GetActives())
            {
                sm = _stockMetaProv.Get(marketMeta.ID, symbol);

                if (sm != null && sm.marketCurrency == optMarketCurrency)
                    return sm;
            }
        }

        // 3) Otherwise go thru markets and see if any has that ticker (first match wins!)
        foreach (MarketMeta marketMeta in _marketMetaProv.GetActives())
        {
            sm = _stockMetaProv.Get(marketMeta.ID, symbol);

            if (sm != null)
                return sm;
        }

        // 4) Lets see just inc has mapping so closed that user has market to be replaced 
        return _stockMetaProv.Get(MarketId.CLOSED, symbol);
    }

    public IReadOnlyCollection<StockMeta> FindStocksList(string search, MarketId marketId = MarketId.Unknown)
    {
        IEnumerable<StockMeta> allStocks = _stockMetaProv.GetAll(marketId);

        if (string.IsNullOrWhiteSpace(search))
            return allStocks.ToList().AsReadOnly();

        List<StockMeta> retSearch = new();

        // 1) Perfect match to ticker (ignore case) is ALWAYS first to be shown

        if (allStocks.Any(s => s.symbol == search.ToUpper()) == true)
            retSearch.Add(allStocks.First(s => s.symbol.ToUpper() == search.ToUpper()));

        // 2) Contains search match per Tickers (ignore case)

        List<StockMeta> searchTicker = allStocks.Where(x => x.symbol.IndexOf(search, StringComparison.InvariantCultureIgnoreCase) >= 0).ToList();

        retSearch.AddRange(searchTicker);

        // 3) Contains search match per Company Name (ignore case)

        List<StockMeta> searchName = allStocks.Where(x => x.name.IndexOf(search, StringComparison.InvariantCultureIgnoreCase) >= 0).ToList();

        retSearch.AddRange(searchName);

        return retSearch.Distinct().ToList().AsReadOnly();
    }

    public async Task<StockMeta[]> FindStockExtAsync(string symbol, MarketId optMarketId = MarketId.Unknown, CurrencyId optMarketCurrency = CurrencyId.Unknown)
    {
        return await _fetchEod.FindBySymbolAsync(symbol, optMarketId, optMarketCurrency);
    }

    public IReadOnlyCollection<SPortfolio> GetPortfolios()
    {
        return _clientStalker.Portfolios().AsReadOnly();
    }

    public ReadOnlyCollection<SAlarm> StockAlarmList(MarketId marketId, string symbol)
    {
        return _clientStalker.StockAlarms($"{marketId}${symbol}");
    }

    public ReadOnlyCollection<SOrder> StockOrderList(string pfName, MarketId marketId, string symbol)
    {
        SPortfolio portfolio = _clientStalker.PortfolioRef(pfName);

        if (pfName == null)
            return new List<SOrder>().AsReadOnly();

        return portfolio.StockOrders.Where(o => o.SRef == $"{marketId}${symbol}").ToList().AsReadOnly();
    }

    public string[] GetSectorNames()
    {   // To support simpler logic on UI, all is either string or string.Empty
        string[] ret = new string[SSector.MaxSectors];

        for (int s = 0; s < SSector.MaxSectors; s++)
        {
            (ret[s], _) = _clientStalker.GetSector(s);

            if (ret[s] == null)
                ret[s] = string.Empty;
        }

        return ret;
    }

    public string[] GetSectorFieldNames(int sectorId)
    {   // To support simpler logic on UI, all is either string or string.Empty
        string[] ret;

        (_, ret) = _clientStalker.GetSector(sectorId);

        if (ret == null || ret.Length == 0)
            // Lets not return UI a empty tables but one w enough records, even if null ones
            ret =  new string[SSector.MaxFields];

        for (int f = 0; f < SSector.MaxFields; f++)
            if (ret[f] == null)
                ret[f] = string.Empty;

        return ret;
    }

    public string[] GetStockSectorFields(string sRef)
    {
        return _clientStalker.GetStockSectors(sRef);
    }
}
