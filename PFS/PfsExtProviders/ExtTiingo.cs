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
using CsvHelper;
using System.Globalization;
using System.Runtime.Serialization;
using static Pfs.Types.IExtDataProvider;

namespace Pfs.ExtProviders;

public class ExtTiingo: IExtProvider, IExtDataProvider // All functions retested manually 2021-Dec-7th
{
    protected readonly IPfsStatus _pfsStatus;

    protected string _error = string.Empty;

    protected string _tiingoApiKey = "";

    /* Tiingo: (Still 2021-Nov-3st, doesnt work w WASM, even w https activated on developer studio testing)
     * 
     * - As of 2021-Jun.. forget TSX / Canadian stock market.. just focus US ones w Tiingo.. did try 'search' cant see their canadian tickers
     * 
     * - Has Intraday functionality, that seams to be working ok... except doesnt wanna work on WASM!
     * 
     * - Hour limit of max 500 queries, Month limit is max 500 unique tickers !very nice! and month unlimited is 10$ only!
     * 
     * => TODO! Would be pretty perfect Intraday tool, if just would work on WASM :/ Try again later! 10$ per month would not be bad 
     *      => But without Toronto, without WASM .. blaah.. anyway all nice as Free account even for US stocks so retry WASM            !!!LATER!!!
     * 
     * (can download list of supported_tickers:https://apimedia.tiingo.com/docs/tiingo/daily/supported_tickers.zip)
     */

    public ExtTiingo(IPfsStatus pfsStatus)
    {
        _pfsStatus = pfsStatus;
    }

    public string GetLastError() { return _error; }

    public void SetPrivateKey(string key)
    {
        _tiingoApiKey = key;
    }

    public int GetLastCreditPrice() { return 0; }

    public bool IsSupport(ProvFunctionId funcId)
    {
        switch (funcId)
        {
            case ProvFunctionId.LatestEod:
            case ProvFunctionId.HistoryEod:
                return true;
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
                return true;
        }
        return false;
    }

    public int GetBatchSizeLimit(ProvFunctionId limitID)
    {
        switch (limitID)
        {
            case ProvFunctionId.LatestEod: return 1;
            case ProvFunctionId.HistoryEod: return 1;
        }
        Log.Warning($"ExtMarketDataTiingo():Limit({limitID}) missing implementation");
        return 1;
    }


#if false // Search 

            // THIS FORMAT WORKS!

            HttpResponseMessage respSearch = await tempHttpClient.GetAsync($"https://api.tiingo.com/tiingo/utilities/search/apple?token={_tiingoApiKey}&format=csv");

            if (respSearch.IsSuccessStatusCode == true)
            {
                var searchRet = await respSearch.Content.ReadAsStringAsync();
            }

            // THIS DOESNT AS OF 19th Jun 2021

            HttpResponseMessage respSearch = await tempHttpClient.GetAsync($"https://api.tiingo.com/tiingo/utilities/search?query=apple?token={_tiingoApiKey}&format=csv");

            if (respSearch.IsSuccessStatusCode == true)
            {
                var searchRet = await respSearch.Content.ReadAsStringAsync();
            }
#endif

#if false // INTRADAY !


    // ? https://api.tiingo.com/documentation/iex => https://api.tiingo.com/iex/<ticker>

    
            HttpResponseMessage respIntra = await tempHttpClient.GetAsync($"https://api.tiingo.com/iex/{tickers[0]}?token={_tiingoApiKey}&format=csv");

            if (respIntra.IsSuccessStatusCode == true)
            {
                var searchRet = await respIntra.Content.ReadAsStringAsync();
            }



ticker,askPrice,askSize,bidPrice,bidSize,high,last,lastSize,lastSaleTimestamp,low,mid,open,prevClose,quoteTimestamp,timestamp,tngoLast,volume
MSFT,261.080000,300,261.060000,126,262.290000,261.080000,100,2021-06-18T15:45:51.552978927-04:00,258.850000,261.070000,260.580000,260.900000,2021-06-18T15:45:52.823840008-04:00,2021-06-18T15:45:52.823840008-04:00,261.070000,604042

#endif

    public Task<Dictionary<string, FullEOD>> GetIntradayAsync(MarketId marketId, List<string> tickers)
    {
        _error = "GetIntradayAsync() - Not supported";
        return null;
    }

    public async Task<Dictionary<string, FullEOD>> GetEodLatestAsync(MarketId marketId, List<string> tickers)
    {
        _error = string.Empty;

        if (string.IsNullOrEmpty(_tiingoApiKey) == true)
        {
            _error = "Tiingo:GetEodLatestAsync() Missing private access key!";
            return null;
        };

        if (tickers.Count != 1)
        {
            _error = "Failed, has 1 ticker on time limit!";
            Log.Warning("Tiingo:GetEodLatestAsync() " + _error);
            return null;
        }

        try
        {
            HttpClient tempHttpClient = new HttpClient();

            // Doesnt work with WASM, even works perfectly from backend code. CORS restrictions their side? see F12... do they see source as localhost? And its prevented?
            HttpResponseMessage resp = await tempHttpClient.GetAsync($"https://api.tiingo.com/tiingo/daily/{tickers[0]}/prices?token={_tiingoApiKey}&format=csv");

            if (resp.IsSuccessStatusCode == false)
            {
                _error = string.Format("Failed: {0} for [[{1}]]", resp.StatusCode.ToString(), tickers[0]);
                Log.Warning("Tiingo:GetEodLatestAsync() " + _error);
                return null;
            }

            string content = await resp.Content.ReadAsStringAsync();
            content = "garbage" + content;                          // As 2021-Nov-3th, this is valid as comes with ",field,field.." on header 
            var csv = new CsvReader(new StringReader(content), CultureInfo.InvariantCulture);
            var dailyItems = csv.GetRecords<TiingoDailyLatestFormat>().ToList();

            if (dailyItems == null || dailyItems.Count != 1)
            {
                _error = string.Format("Failed, empty data: {0} for [[{1}]]", resp.StatusCode.ToString(), tickers[0]);
                Log.Warning("Tiingo:GetEodLatestAsync() " + _error);
                return null;
            }

            Dictionary<string, FullEOD> ret = new Dictionary<string, FullEOD>();

            ret[tickers[0]] = new FullEOD()
            {
                Date = new DateOnly(dailyItems[0].date.Year, dailyItems[0].date.Month, dailyItems[0].date.Day),
                Close = dailyItems[0].close,
                High = dailyItems[0].high,
                Low = dailyItems[0].low,
                Open = dailyItems[0].open,
                PrevClose = -1,
                Volume = dailyItems[0].volume,
            };

            return ret;
        }
        catch ( Exception e )
        {
            _error = string.Format("Failed, connection exception {0} for [[{1}]]", e.Message, tickers[0]);
            Log.Warning("Tiingo:GetEodLatestAsync() " + _error);
        }
        return null;
    }

    public async Task<Dictionary<string, List<FullEOD>>> GetEodHistoryAsync(MarketId marketId, List<string> tickers, DateTime startDay, DateTime endDay)
    {
        _error = string.Empty;

        if (string.IsNullOrEmpty(_tiingoApiKey) == true)
        {
            _error = "Tiingo:GetEodHistoryAsync() Missing private access key!";
            return null;
        }
        if (tickers.Count != 1)
        {
            _error = "Filed, has 1 ticker on time limit!";
            Log.Warning("Tiingo:GetEodLatestAsync() " + _error);
            return null;
        }

        string start = startDay.ToString("yyyy-MM-dd");
        string end = endDay.ToString("yyyy-MM-dd");

        try
        {
            HttpClient tempHttpClient = new HttpClient();
            HttpResponseMessage resp = await tempHttpClient.GetAsync($"https://api.tiingo.com/tiingo/daily/{tickers[0]}/prices?startDate={start}&endDate={end}&format=csv&resampleFreq=daily&token={_tiingoApiKey}");

            if (resp.IsSuccessStatusCode == false)
            {
                _error = string.Format("Failed: {0} for [[{1}]]", resp.StatusCode.ToString(), tickers[0]);
                Log.Warning("Tiingo:GetEodHistoryAsync() " + _error);
                return null;
            }

            string content = await resp.Content.ReadAsStringAsync();
            var csv = new CsvReader(new StringReader(content), CultureInfo.InvariantCulture);
            var stockRecords = csv.GetRecords<TiingoHistoryFormat>().ToList();

            if (stockRecords == null || stockRecords.Count == 0)
            {
                _error = string.Format("Failed, empty data: {0} for [[{1}]]", resp.StatusCode.ToString(), tickers[0]);
                Log.Warning("Tiingo:GetEodHistoryAsync() " + _error);
                return null;
            }

            Dictionary<string, List<FullEOD>> ret = new Dictionary<string, List<FullEOD>>();

            List<FullEOD> ext = stockRecords.ConvertAll(s => new FullEOD()
            {
                Date = DateOnly.FromDateTime(s.date),
                Close = s.close,
                High = s.high,
                Low = s.low,
                Open = s.open,
                PrevClose = -1,
                Volume = s.volume,
            });

            ret.Add(tickers[0], ext);

            return ret;
        }
        catch (Exception e)
        {
            _error = string.Format("Failed, connection exception {0} for [[{1}]]", e.Message, tickers[0]);
            Log.Warning("Tiingo:GetEodLatestAsync() " + _error);
        }
        return null;
    }

    /*
    ,adjClose,adjHigh,adjLow,adjOpen,adjVolume,close,date,divCash,high,low,open,splitFactor,volume
    0,17.0,17.16,16.91,16.99,17613186,17.0,2021-04-27T00:00:00+00:00,0.0,17.16,16.91,16.99,1.0,17613186
     */

    [DataContract]
    private class TiingoDailyLatestFormat
    {
        [DataMember] public decimal garbage { get; set; }
        [DataMember] public decimal adjClose { get; set; }
        [DataMember] public decimal adjHigh { get; set; }
        [DataMember] public decimal adjLow { get; set; }
        [DataMember] public decimal adjOpen { get; set; }
        [DataMember] public int adjVolume { get; set; }
        [DataMember] public decimal close { get; set; }
        [DataMember] public DateTime date { get; set; }
        [DataMember] public decimal divCash { get; set; }
        [DataMember] public decimal high { get; set; }
        [DataMember] public decimal low { get; set; }
        [DataMember] public decimal open { get; set; }
        [DataMember] public decimal splitFactor { get; set; }
        [DataMember] public int volume { get; set; }
    }

    /*
    date,close,high,low,open,volume,adjClose,adjHigh,adjLow,adjOpen,adjVolume,divCash,splitFactor
    2021-04-01,16.84,16.84,16.49,16.66,13316794,16.84,16.84,16.49,16.66,13316794,0.0,1.0
    */

    [DataContract]
    private class TiingoHistoryFormat
    {
        [DataMember] public DateTime date { get; set; }
        [DataMember] public decimal close { get; set; }
        [DataMember] public decimal high { get; set; }
        [DataMember] public decimal low { get; set; }
        [DataMember] public decimal open { get; set; }
        [DataMember] public int volume { get; set; }
        [DataMember] public decimal adjClose { get; set; }
        [DataMember] public decimal adjHigh { get; set; }
        [DataMember] public decimal adjLow { get; set; }
        [DataMember] public decimal adjOpen { get; set; }
        [DataMember] public int adjVolume { get; set; }
        [DataMember] public decimal divCash { get; set; }
        [DataMember]public decimal splitFactor { get; set; }
    }
}
