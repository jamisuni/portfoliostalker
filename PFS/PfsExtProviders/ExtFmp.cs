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

using Serilog;

using Pfs.Types;
using static Pfs.Types.IExtDataProvider;

using System.Text.Json;

namespace Pfs.ExtProviders;

// (https://site.financialmodelingprep.com/) Very extensive API, covering hefty amount of different use cases. Very Impressive!
public class ExtFmp : IExtProvider, IExtDataProvider
{
    protected readonly IPfsStatus _pfsStatus;

    protected string _error = string.Empty;

    protected string _fmpApiKey = "";

    /* Provided functionalities: superb market support!
     * 
     * StockList-ExchangeSymbols: 'https://financialmodelingprep.com/api/v3/symbol/{marketId}?apikey=' 
     *  - Return all need details for *ALL* symbols on market, this is getting very fast very heavy size content to 
     *    process for major markets, but very tempting for small markets like HEL (symbols BITTI.HE example)
     *  
     * CompanyInformation-Holidays: 'https://financialmodelingprep.com/api/v3/is-the-market-open?exchange=EURONEXT&apikey='
     *  - Would allow automatically find holidays for year, as atm manually entering them
     *  
     * CompanyInformation-AllAvailableExchanges: 'https://financialmodelingprep.com/api/v3/exchanges-list'
     *  - Naming thats used needs to always market & symbol conversion
     *  
     * Quote-FullQuote: 'https://financialmodelingprep.com/api/v3/quote/AAPL?apikey='
     *  - Single stock on time... just remember when market open this jumps to open time valuations, not latest EOD
     * 
     * Missing atm:
     *  - Major! Way to know sure fetch result is end of day closing value for actual market hours -> may has to start bringing expected date in :/
     *  - Minor! Way to get EOD details for batch of symbols on one fetch... cant see one, but then not a biggie
     */

    public ExtFmp(IPfsStatus pfsStatus)
    {
        _pfsStatus = pfsStatus;
    }

    public string GetLastError() { return _error; }

    public void SetPrivateKey(string key)
    {
        _fmpApiKey = key;
    }

    public int GetLastCreditPrice() { return 1; }

    public bool IsSupport(ProvFunctionId funcId)
    {
        switch (funcId)
        {
            case ProvFunctionId.LatestEod:
                return true;

            case ProvFunctionId.Intraday:
                return false; // has actually good one, but later feature for meh

            case ProvFunctionId.HistoryEod:
                return false; // havent look yet
        }
        return false;
    }

    public bool IsMarketSupport(MarketId marketId)
    {
        return MarketSupport(marketId);
    }

    public static bool MarketSupport(MarketId marketId)
    {
        switch (marketId)
        {
            case MarketId.NASDAQ:
            case MarketId.NYSE:
            case MarketId.AMEX:
            case MarketId.TSX:
            case MarketId.TSXV:
            case MarketId.XETRA:
            case MarketId.OMXH:
            case MarketId.OMX:
            case MarketId.LSE:
                return true;
        }
        return false;
    }

    public int GetBatchSizeLimit(ProvFunctionId limitID)
    {
        switch (limitID)
        {
            case ProvFunctionId.LatestEod: return _maxTickers;
            case ProvFunctionId.HistoryEod: return 1;
            case ProvFunctionId.Intraday: return _maxTickers;
        }
        Log.Warning($"FMP:Limit({limitID}) missing implementation");
        return 1;
    }

    const int _maxTickers = 1;

    public async Task<Dictionary<string, FullEOD>> GetIntradayAsync(MarketId marketId, List<string> tickers)
    {
        _error = string.Empty;

        return null;
    }

    // https://financialmodelingprep.com/api/v3/symbol/NASDAQ?apikey=xSDJ33RJggIjt45BOsuAkg8LFgFp00KB

    // This looks superb powerfull, but doesnt yet fit to model I been using to do fetching. So starting first
    // with caching market response to bit see how well API data is trustable.. and if all goes work then need
    // to start doing modifications to support wider search possibilities and priorities.

    protected Dictionary<MarketId, SymbolLatestData[]> _cachedMarkets = new();

    public async Task<Dictionary<string, FullEOD>> GetEodLatestAsync(MarketId marketId, List<string> tickers)
    {
        _error = string.Empty;

        if (string.IsNullOrEmpty(_fmpApiKey) == true)
        {
            _error = "FMP::GetEodLatestAsync() Missing private access key!";
            return null;
        }

        int amountOfReqTickers = tickers.Count();

        string symbolEnding = FmpSymbolEnding(marketId);
        string marketTag = FmpMarketTag(marketId);
        string symbol = tickers[0];

        if (FmpFullMarketFetch(marketId))
        {
            try
            {
                if (_cachedMarkets.ContainsKey(marketId) == false)
                {
                    HttpClient tempHttpClient = new HttpClient();
                    HttpResponseMessage resp = await tempHttpClient.GetAsync($"https://financialmodelingprep.com/api/v3/symbol/{marketTag}?apikey={_fmpApiKey}");

                    if (resp.IsSuccessStatusCode == false)
                    {
                        _error = $"FMP failed: {resp.StatusCode} for [[{marketId}]]";
                        Log.Warning("FMP::GetEodLatestAsync() " + _error);
                        return null;
                    }

                    string content = await resp.Content.ReadAsStringAsync();
                    SymbolLatestData[] symbolsData = JsonSerializer.Deserialize<SymbolLatestData[]>(content);

                    _cachedMarkets.Add(marketId, symbolsData);
                }

                Dictionary<string, FullEOD> ret = new Dictionary<string, FullEOD>();

                FullEOD eod = Local_GetCachedFullEOD(marketId, symbol+symbolEnding);

                if (eod != null)
                    ret.Add(symbol, eod);

                if (ret.Count() == 0)
                {
                    _error = $"Failed, empty data for [[{marketId}]]";
                    Log.Warning("FMP::GetEodLatestAsync() " + _error);
                    return null;
                }
                else if (ret.Count() < amountOfReqTickers)      // !!!USELESS!!!
                {
                    _error = $"Warning, requested {amountOfReqTickers} got just {ret.Count()} for [[{marketId}]]";
                    Log.Warning("FMP::GetEodLatestAsync() " + _error);
                    // This is just warning, still got data so going to go processing it...
                }

                return ret;
            }
            catch (Exception e)
            {
                _error = $"Failed! Connection exception {e.Message} for [[{marketId}]]";
                Log.Warning("FMP::GetEodLatestAsync() " + _error);
            }
        }
        else
        {
            try
            {
                HttpClient tempHttpClient = new HttpClient();
                HttpResponseMessage resp = await tempHttpClient.GetAsync($"https://financialmodelingprep.com/api/v3/quote/{symbol + symbolEnding}?apikey={_fmpApiKey}");

                if (resp.IsSuccessStatusCode == false)
                {
                    _error = $"FMP failed: {resp.StatusCode} for [[{marketId}${symbol}]]";
                    Log.Warning("FMP::GetEodLatestAsync() " + _error);
                    return null;
                }

                string content = await resp.Content.ReadAsStringAsync();
                SymbolLatestData[] symbolsData = JsonSerializer.Deserialize<SymbolLatestData[]>(content);

                Dictionary<string, FullEOD> ret = new Dictionary<string, FullEOD>();

                SymbolLatestData data = symbolsData.FirstOrDefault(s => s.symbol.Equals(symbol + symbolEnding));

                ret.Add(symbol, Local_ConvertFullEOD(data));

                if (ret.Count() == 0)
                {
                    _error = $"Failed, empty data for [[{marketId}]]";
                    Log.Warning("FMP::GetEodLatestAsync() " + _error);
                    return null;
                }
                return ret;
            }
            catch (Exception e)
            {
                _error = $"Failed! Connection exception {e.Message} for [[{marketId}]]";
                Log.Warning("FMP::GetEodLatestAsync() " + _error);
            }

        }
        return null;

        FullEOD Local_GetCachedFullEOD(MarketId marketId, string symbol)
        {
            if (_cachedMarkets.ContainsKey(marketId) == false)
                return null;

            SymbolLatestData data = _cachedMarkets[marketId].FirstOrDefault(s => s.symbol == symbol);

            if (data == null || data.price.HasValue == false || data.timestamp.HasValue == false)
                return null;

            return Local_ConvertFullEOD(data);
        }

        FullEOD Local_ConvertFullEOD(SymbolLatestData data)
        {
            return new FullEOD()
            {
                Date = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(data.timestamp.Value).DateTime),
                Close = data.price.Value,
                Open = data.open ?? -1,
                High = data.dayHigh ?? -1,
                Low = data.dayLow ?? -1,
                PrevClose = data.previousClose ?? -1,
                Volume = (int)data.volume,
            };
        }
    }

    public async Task<Dictionary<string, List<FullEOD>>> GetEodHistoryAsync(MarketId marketId, List<string> tickers, DateTime startDay, DateTime endDay)
    {
        _error = string.Empty;

        return null;
    }

    static protected bool FmpFullMarketFetch(MarketId marketId)
    {
        switch (marketId)
        {
            case MarketId.OMX:
            case MarketId.OMXH:
                return true;

            case MarketId.TSX:
            case MarketId.XETRA:
            case MarketId.TSXV:
                return true; // maybe, lets see

            case MarketId.LSE:
                return false; // too heavy
        }
        return false;
    }

    static protected string FmpSymbolEnding(MarketId marketId)
    {
        switch (marketId)
        {
            case MarketId.LSE: return ".L";
            case MarketId.OMX: return ".ST";
            case MarketId.OMXH: return ".HE";
            case MarketId.TSX: return ".TO";
            case MarketId.TSXV: return ".V";
            case MarketId.XETRA: return ".F";
        }
        return "";
    }

    static protected string FmpMarketTag(MarketId marketId)
    {
        switch (marketId)
        {
            case MarketId.LSE: return "LSE";
            case MarketId.OMX: return "STO";
            case MarketId.OMXH: return "HEL";
            case MarketId.TSX: return "TSX";
            case MarketId.TSXV: return "TSXV";
            case MarketId.XETRA: return "XETRA";
        }
        return "";
    }

    // !!!REMEMBER!!! JSON!!! https://json2csharp.com/json-to-csharp

    /*
  {
    "symbol": "AAPL",
    "name": "Apple Inc.",
    "price": 222.91,
    "changesPercentage": -1.328,
    "change": -3,
    "dayLow": 220.28,
    "dayHigh": 225.34,
    "yearHigh": 237.49,
    "yearLow": 164.08,
    "marketCap": 3389145931000,
    "priceAvg50": 227.1662,
    "priceAvg200": 201.98105,
    "exchange": "NASDAQ",
    "volume": 60699805,
    "avgVolume": 50353639,
    "open": 220.965,
    "previousClose": 225.91,
    "eps": 6.58,
    "pe": 33.88,
    "earningsAnnouncement": "2024-10-31T04:00:00.000+0000",
    "sharesOutstanding": 15204100000,
    "timestamp": 1730491201
  },
     */

    public class SymbolLatestData
    {
        public string symbol { get; set; }
        public string name { get; set; }
        public decimal? price { get; set; }
        public decimal? changesPercentage { get; set; }
        public decimal? change { get; set; }
        public decimal? dayLow { get; set; }
        public decimal? dayHigh { get; set; }
        public decimal? yearHigh { get; set; }
        public decimal? yearLow { get; set; }
        public long? marketCap { get; set; }
        public decimal? priceAvg50 { get; set; }
        public decimal? priceAvg200 { get; set; }
        public string exchange { get; set; }
        public decimal? volume { get; set; }
        public decimal? avgVolume { get; set; }
        public decimal? open { get; set; }
        public decimal? previousClose { get; set; }
        public decimal? eps { get; set; }
        public decimal? pe { get; set; }
        public string earningsAnnouncement { get; set; }
        public long? sharesOutstanding { get; set; }
        public int? timestamp { get; set; }
    }
}
