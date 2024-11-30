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

using System.Runtime.Serialization;
using System.Globalization;

using Serilog;
using CsvHelper;

using Pfs.Types;
using static Pfs.Types.IExtDataProvider;

namespace Pfs.ExtProviders;

// Unibit provides market data for all main markets
public class ExtUnibit : IExtProvider, IExtDataProvider
{
    protected string _error = string.Empty;
    protected int _creditCost = 0;

    protected string _unibitApiKey = "";


    /* Provided functionalities: !ONE OF BEST ONES FOR EOD (2H after) W SUPERB MARKET COVERAGE!
     * 
     *  - With Free Account 50,000 credits per month means 2170 credits per day (23 is max mon-fri per month)
     *    but as it uses 10 credits per one latest EOD => about 200 stocks maximum ... all strong fast for many markets!
     *  - First payment account is 200$/m but allows usage on application, as long as not selling data sets
     *  - Nice option for WASM Clients (personal usage), as fast fetch, good coverage of markets
     *  
     * - NASDAQ/NYSE nicely 3 hours after closing
     * - Also TSX & HEL etc works with this one
     * 
     * Has company lists per market: But with 1 credit per company, would need to be carefull w free account.
     * 
     * Divident:
     * - Maybe try later better, did try pull LUMN for 2021, did get last historical divident but not next ones information,
     *   could be because next one not yet decided. Anyway cost is 100 points of credit for single line reply.. so cant even test properly :/
     * 
     * Per https://unibit.ai/pricing:
     * - "The general rule of thumb is that you may not resell or redistribute our datasets. Any research, analytics or 
     *    application built upon our datasets are generally permitted." -> actually having server would break this, so hmm, forget 199
     */

    // https://unibit.ai/api/docs/V2.0/historical_stock_price
    // => This API is updated every day no later than 2 hours after the market closes in local time.

    public string GetLastError() { return _error; }

    public void SetPrivateKey(string key)
    {
        _unibitApiKey = key;
    }

    public int GetLastCreditPrice() 
    { 
        return _creditCost; 
    }

    public bool IsMarketSupport(MarketId marketId)
    {
        return MarketSupport(marketId);
    }

    public bool IsSupport(ProvFunctionId funcId)
    {
        switch (funcId)
        {
            case ProvFunctionId.LatestEod:
                return true;

            case ProvFunctionId.Intraday:
                return false;  // No support at all on API

            case ProvFunctionId.HistoryEod:
                return false; // pricy as heck! NO-GO
        }
        return false;
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
            case MarketId.OMX:
            case MarketId.OMXH:
            case MarketId.XETRA:    // but no ETFs
            case MarketId.LSE:
                return true;
        }
        return false;
    }

    const int _maxTickers = 50; // 50 is API defined maximum, so running full speed...

    public int GetBatchSizeLimit(ProvFunctionId limitID)
    {
        switch (limitID)
        {
            case ProvFunctionId.LatestEod: return _maxTickers;

            case ProvFunctionId.HistoryEod: return 1;
        }
        Log.Warning("ExtMarketDataUNIBIT():GetBatchSizeLimit({0}) missing implementation", limitID.ToString());
        return 1;
    }

    public Task<Dictionary<string, FullEOD>> GetIntradayAsync(MarketId marketId, List<string> tickers)
    {
        _creditCost = 0;
        _error = "GetIntradayAsync() - Not supported";
        return null;
    }

    public async Task<Dictionary<string, FullEOD>> GetEodLatestAsync(MarketId marketId, List<string> tickers)
    {
        _error = string.Empty;
        _creditCost = 0;

        if (string.IsNullOrEmpty(_unibitApiKey) == true)
        {
            _error = "UNIBIT::Missing private access key!";
            return null;
        }

        int amountOfReqTickers = tickers.Count();

        string unibitJoinedTickers = ExtMarketSuppUNIBIT.JoinPfsTickers(marketId, tickers, _maxTickers);

        if (string.IsNullOrEmpty(unibitJoinedTickers) == true)
        {
            _error = "UNIBIT::Failed, over ticker limit";
            Log.Warning(_error);
            return null;
        }

        _creditCost = amountOfReqTickers * 10;

        try
        {
            HttpClient tempHttpClient = new HttpClient();
            HttpResponseMessage resp = await tempHttpClient.GetAsync($"https://api.unibit.ai/v2/stock/historical/?tickers={unibitJoinedTickers}&dataType=csv&accessKey={_unibitApiKey}");

            if (resp.IsSuccessStatusCode == false)
            {
                _error = $"UNIBIT::Failed: StatusCode={resp.StatusCode} for [[{unibitJoinedTickers}]]";
                Log.Warning(_error);
                return null;
            }

            string content = await resp.Content.ReadAsStringAsync();
            var csv = new CsvReader(new StringReader(content), CultureInfo.InvariantCulture);
            var dailyItems = csv.GetRecords<UnibitDailyFormat>().ToList();

            Log.Information($"UNIBIT:OK RESP? {unibitJoinedTickers} content=[{content}]");

            if (dailyItems == null || dailyItems.Count() == 0)
            {
                _error = $"UNIBIT::Failed, empty data! For [[{unibitJoinedTickers}]]";
                Log.Warning(_error);
                return null;
            }
            else if (dailyItems.Count() < amountOfReqTickers)
            {
                _error = $"UNIBIT::Warning, requested {amountOfReqTickers} got just {dailyItems.Count()} for [[{unibitJoinedTickers}]]";
                Log.Warning(_error);

                // This is just warning, still got data so going to go processing it...
            }

            // All seams to be enough well so lets convert data to PFS format

            Dictionary<string, FullEOD> ret = new Dictionary<string, FullEOD>();

            foreach (var item in dailyItems)
            {
                string pfsTicker = ExtMarketSuppUNIBIT.TrimToPfsTicker(marketId, item.ticker);

                ret[pfsTicker] = new FullEOD()
                {
                    Date = new DateOnly(item.date.Year, item.date.Month, item.date.Day),
                    Close = item.close,
                    High = item.high,
                    Low = item.low,
                    Open = item.open,
                    PrevClose = -1,
                    Volume = item.volume,
                };
            }

            return ret;
        }
        catch ( Exception e )
        {
            _error = string.Format("UNIBIT::Failed! Connection exception {0} for [[{1}]]", e.Message, unibitJoinedTickers);
            Log.Warning(_error);
        }
        return null;
    }

    public async Task<Dictionary<string, List<FullEOD>>> GetEodHistoryAsync(MarketId marketId, List<string> tickers, DateTime startDay, DateTime endDay)    // !!!IMPORTANT!!! Pricy as heck, should not be used!!!
    {
#if false
        _creditCost = 0;
        _error = string.Empty;

        if (string.IsNullOrEmpty(_unibitApiKey) == true)
        {
            _error = "UNIBIT::GetEodHistoryAsync() Missing private access key!";
            return null;
        }

        var unibitJoinedTickers = ExtMarketSuppUNIBIT.JoinPfsTickers(marketId, tickers, _maxTickers);
        
        if (string.IsNullOrEmpty(unibitJoinedTickers) == true)
        {
            _error = "Failed, over ticker limit!";
            Log.Warning("UNIBIT::GetEodHistoryAsync() " + _error);
            return null;
        }

        string start = startDay.ToString("yyyy-MM-dd");
        string end = endDay.ToString("yyyy-MM-dd");

        try
        {
            HttpClient tempHttpClient = new HttpClient();
            HttpResponseMessage resp = await tempHttpClient.GetAsync($"https://api.unibit.ai/v2/stock/historical/?tickers={unibitJoinedTickers}&dataType=csv&startDate={start}&endDate={end}&accessKey={_unibitApiKey}");

            if (resp.IsSuccessStatusCode == false)
            {
                _error = string.Format("Failed: {0} for [[{1}]]", resp.StatusCode.ToString(), unibitJoinedTickers);
                Log.Warning("UNIBIT:GetEodHistoryAsync() " + _error);
                return null;
            }

            string content = await resp.Content.ReadAsStringAsync();
            var csv = new CsvReader(new StringReader(content), CultureInfo.InvariantCulture);
            var stockRecords = csv.GetRecords<UnibitDailyFormat>().ToList();

            if (stockRecords == null || stockRecords.Count() == 0)
            {
                _error = string.Format("Failed, empty data: {0} for [[{1}]]", resp.StatusCode.ToString(), unibitJoinedTickers);
                Log.Warning("UNIBIT:GetEodHistoryAsync() " + _error);
                return null;
            }

            Dictionary<string, List<FullEOD>> ret = new Dictionary<string, List<FullEOD>>();

            int receivedDataAmount = 0;
            foreach (string pfsTicker in tickers)
            {
                string unibitTicker = ExtMarketSuppUNIBIT.ExpandToUnibitTicker(marketId, pfsTicker);

                List<UnibitDailyFormat> partial = stockRecords.Where(s => s.ticker == unibitTicker).ToList();

                if (partial.Count() == 0)
                {
                    // this one didnt receive any data
                    Log.Warning("UNIBIT:GetEodHistoryAsync() no data for {0}", pfsTicker);
                    continue;
                }

                List<FullEOD> ext = partial.ConvertAll(s => new FullEOD()
                {
                    Date = DateOnly.FromDateTime(s.date),
                    Close = s.close,
                    High = s.high,
                    Low = s.low,
                    Open = s.open,
                    PrevClose = -1,
                    Volume = s.volume,
                });

                ext.Reverse();
                ret.Add(pfsTicker, ext);

                receivedDataAmount++;
            }

            if (receivedDataAmount < tickers.Count())
            {
                _error = string.Format("Warning, requested {0} got {1} for [[{2}]]", tickers.Count(), receivedDataAmount, unibitJoinedTickers);
                Log.Warning("UNIBIT:GetEodHistoryAsync() " + _error);
            }

            return ret;
        }
        catch (Exception e)
        {
            _error = string.Format("Failed, connection exception {0} for [[{1}]]", e.Message, unibitJoinedTickers);
            Log.Warning("UNIBIT::GetEodHistoryAsync() " + _error);
        }
#endif
        return null;
    }

    /*
        ticker,date,open,high,low,close,adj close,volume
        LUMN,2021-03-15,14.13,14.33,14.015,14.19,14.19,8842219
        MSFT,2021-03-15,234.96,235.185,231.82,234.81,234.81,25970922
     */

    [DataContract]
    private class UnibitDailyFormat
    {
        [DataMember]
        public string ticker { get; set; }

        [DataMember]
        public DateTime date { get; set; }

        [DataMember]
        public decimal open { get; set; }

        [DataMember]
        public decimal high { get; set; }

        [DataMember]
        public decimal low { get; set; }

        [DataMember]
        public decimal close { get; set; }

        [DataMember]
        public int volume { get; set; }
    }
}

public class ExtMarketSuppUNIBIT
{
    static public string TrimToPfsTicker(MarketId marketId, string unibitTicker)
    {
        string unibitTickerEnding = UnibitTickerEnding(marketId);

        if (string.IsNullOrWhiteSpace(unibitTickerEnding) == false && unibitTicker.EndsWith(unibitTickerEnding) == true)
            return unibitTicker.Substring(0, unibitTicker.Length - unibitTickerEnding.Length);

        return unibitTicker;
    }

    static public string ExpandToUnibitTicker(MarketId marketId, string pfsTicker)
    {
        string unibitTickerEnding = UnibitTickerEnding(marketId);

        if (string.IsNullOrWhiteSpace(unibitTickerEnding) == false && pfsTicker.EndsWith(unibitTickerEnding) == false)
            return pfsTicker + unibitTickerEnding;

        return pfsTicker;
    }

    static public string JoinPfsTickers(MarketId marketId, List<string> pfsTickers, int maxTickers)
    {
        // Up to 50 stock quotes can be requested at a time. (https://unibit.ai/api/docs/V2.0/historical_stock_price)

        if (pfsTickers.Count > maxTickers)
            // Coding error, should have divided this task to multiple parts
            return string.Empty;

        return string.Join(',', pfsTickers.ConvertAll<string>(s => ExpandToUnibitTicker(marketId, s)));
    }

    static protected string UnibitTickerEnding(MarketId marketId)
    {
        switch (marketId)
        {
            case MarketId.OMX: return ".ST";
            case MarketId.OMXH: return ".HE";
            case MarketId.TSX: return ".TO";
            case MarketId.XETRA: return ".DE";
            case MarketId.LSE: return ".L";
            case MarketId.TSXV: return ".V";
        }
        return "";
    }
}
