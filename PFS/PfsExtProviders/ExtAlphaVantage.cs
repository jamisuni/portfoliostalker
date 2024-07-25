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

public class ExtAlphaVantage : IExtProvider, IExtDataProvider
{
    /* Promising, potentially lot of usable features.. but free is atm too limited to do testing of additional features.
     * 
     *  => 5 API request per minute
     *  => Single ticker on time
     *  => 25 request per day (used to be 500 before)
     *  => Has latest EOD (Quote Endpoint / GLOBAL_QUOTE)
     *  => Has history (TIME_SERIES_DAILY) (Nice!)
     *  => NASDAQ/NYSE/AMEX available hour after close (or maybe earlier)
     *  => Has intraday
     * 
     * !!!LATER!!! (postponed as cant test w 25 limit): => 50$ would not be bad, I truly like this !PRIORITY TO RETEST THIS AGAIN!
     *  - Give new round of testing for this, and full reread as havent really poke deeper w this one for long time
     *  - Has Forex history
     */

    protected readonly IPfsStatus _pfsStatus;

    protected string _error = string.Empty;
    protected string _alphaVantageApiKey = "";

    public ExtAlphaVantage(IPfsStatus pfsStatus)
    {
        _pfsStatus = pfsStatus;
    }

    public string GetLastError() { return _error; }

    public void SetPrivateKey(string key)
    {
        _alphaVantageApiKey = key;
    }

    public int GetLastCreditPrice() { return 1; } // fixed price

    protected DateTime _lastUseTime = DateTime.UtcNow.AddMinutes(-1);

    public bool IsSupport(ProvFunctionId funcId)
    {
        switch (funcId)
        {
            case ProvFunctionId.LatestEod:
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
            case MarketId.TSX:
            case MarketId.XETRA:
            case MarketId.LSE:
                return true;
        }
        return false; 
    }

    public int GetBatchSizeLimit(ProvFunctionId limitID)
    {
        return 1;
    }

    protected async Task InternalDelayAwait()
    {
        int speedCap = _pfsStatus.GetAppCfg(AppCfgId.AlphaVantageSpeedSecs);

        if (speedCap > 1 && DateTime.UtcNow < _lastUseTime.AddSeconds(speedCap))
        {
            int seconds = (int)(_lastUseTime.AddSeconds(speedCap) - DateTime.UtcNow).TotalSeconds;
            seconds = seconds == 0 ? 1 : seconds > speedCap ? speedCap : seconds;

            await Task.Delay(seconds * 1000);
        }
        _lastUseTime = DateTime.UtcNow;
    }

    public Task<Dictionary<string, FullEOD>> GetIntradayAsync(MarketId marketId, List<string> tickers)
    {
        _error = "GetIntradayAsync() - Supports, but cant use w these limits";
        return null;
    }

    public async Task<Dictionary<string, FullEOD>> GetEodLatestAsync(MarketId marketId, List<string> tickers)
    {
        _error = string.Empty;

        if (string.IsNullOrEmpty(_alphaVantageApiKey) == true)
        {
            _error = "ALPHAV::GetEodLatestAsync() Missing private access key!";
            return null;
        }

        if (tickers.Count != 1)
        {
            _error = "Failed, has 1 ticker on time limit!";
            Log.Warning("ALPHAV:GetEodLatestAsync() " + _error);
            return null;
        }

        string alphaTicker = PfsToAlphaTicker(marketId, tickers[0]);

        await InternalDelayAwait();

        try
        {
            HttpClient tempHttpClient = new HttpClient();

            HttpResponseMessage resp = await tempHttpClient.GetAsync($"https://www.alphavantage.co/query?function=GLOBAL_QUOTE&symbol={alphaTicker}&apikey={_alphaVantageApiKey}&datatype=csv");

            if (resp.IsSuccessStatusCode == false)
            {
                _error = $"AlphaV failed: {resp.StatusCode} for [[{tickers[0]}]]";
                Log.Warning("ALPHAV::GetEodLatestAsync() " + _error);
                return null;
            }

            string content = await resp.Content.ReadAsStringAsync();
            var csv = new CsvReader(new StringReader(content), CultureInfo.InvariantCulture);
            var dailyItem = csv.GetRecords<AlphaVantageQuoteFormat>().ToList();

            if (dailyItem == null || dailyItem.Count() != 1)
            {
                _error = $"Failed, empty data: {resp.StatusCode} for [[{tickers[0]}]]";
                Log.Warning("ALPHAV::GetEodLatestAsync() " + _error);
                return null;
            }

            // All seams to be enough well so lets convert data to PFS format

            Dictionary<string, FullEOD> ret = new Dictionary<string, FullEOD>();

            ret[tickers[0]] = new FullEOD()
            {
                Date = new DateOnly(dailyItem[0].latestDay.Year, dailyItem[0].latestDay.Month, dailyItem[0].latestDay.Day),
                Close = dailyItem[0].price,
                High = dailyItem[0].high,
                Low = dailyItem[0].low,
                Open = dailyItem[0].open,
                PrevClose = dailyItem[0].previousClose,
                Volume = dailyItem[0].volume,
            };

            return ret;
        }
        catch ( Exception e )
        {
            _error = $"AlphaV connection exception {e.Message} for [[{tickers[0]}]]";
            Log.Warning("ALPHAV::GetEodLatestAsync() " + _error);
        }
        return null;
    }

    public async Task<Dictionary<string, List<FullEOD>>> GetEodHistoryAsync(MarketId marketId, List<string> tickers, DateTime startDay, DateTime endDay)
    {
        _error = string.Empty;

        if (string.IsNullOrEmpty(_alphaVantageApiKey) == true)
        {
            _error = "ALPHAV::GetEodHistoryAsync() Missing private access key!";
            return null;
        }

        if (tickers.Count != 1)
        {
            _error = "Failed, has 1 ticker on time limit!";
            Log.Warning("ALPHAV:GetEodLatestAsync() " + _error);
            return null;
        }

        string alphaTicker = PfsToAlphaTicker(marketId, tickers[0]);

        await InternalDelayAwait();

        // By default using same default as they, 100 last.. doesnt seam to effect cost
        string outputsize = "compact";

        if ((endDay - startDay).TotalDays > 50)
            // But even smallest risk of having too little data, we go long...
            outputsize = "full";

        try
        {
            HttpClient tempHttpClient = new HttpClient();

            // Default is compact, and limits to 100 last records (but if needs jumps to full history)
            HttpResponseMessage resp = await tempHttpClient.GetAsync($"https://www.alphavantage.co/query?function=TIME_SERIES_DAILY&symbol={alphaTicker}&apikey={_alphaVantageApiKey}&datatype=csv&outputsize={outputsize}");

            if (resp.IsSuccessStatusCode == false)
            {
                _error = $"Failed: {resp.StatusCode} for [[{tickers[0]}]]";
                Log.Warning("ALPHAV::GetEodHistoryAsync() " + _error);
                return null;
            }

            string content = await resp.Content.ReadAsStringAsync();
            var csv = new CsvReader(new StringReader(content), CultureInfo.InvariantCulture);
            var stockRecords = csv.GetRecords<AlphaVantageDailyFormat>().ToList();

            if (stockRecords == null || stockRecords.Count() == 0)
            {
                _error = $"Failed, empty data: {resp.StatusCode} for [[{tickers[0]}]]";
                Log.Warning("ALPHAV::GetEodHistoryAsync() " + _error);
                return null;
            }

            Dictionary<string, List<FullEOD>> ret = new Dictionary<string, List<FullEOD>>();

            List<FullEOD> ext = stockRecords.ConvertAll(s => new FullEOD()
            {
                Date = new DateOnly(s.timestamp.Year, s.timestamp.Month, s.timestamp.Day),
                Close = s.close,
                High = s.high,
                Low = s.low,
                Open = s.open,
                PrevClose = -1,     // dont start pulling it here from previous if dont have field, let central place to do pulling from prev day
                Volume = s.volume,
            });

            ext.Reverse();
            ret.Add(tickers[0], ext);

            return ret;
        }
        catch (Exception e)
        {
            _error = $"Failed, connection exception {e.Message} for [[{tickers[0]}]]";
            Log.Warning("ALPHAV::GetEodHistoryAsync() " + _error);
        }
        return null;
    }

    protected string PfsToAlphaTicker(MarketId marketId, string ticker)
    {   // Later! Rumor is that if using exactly same format as yahoo financing could access maybe more markets....
        switch (marketId)
        {
            case MarketId.TSX: return ticker + ".TRT";
            case MarketId.XETRA: return ticker + ".DEX"; // checked OK at 2024-Jul
            case MarketId.LSE: return ticker + ".LON";
        }
        return ticker;
    }

#if false // Yep, RSI works returns a lot of history also..
    public async Task TestRSIAsync()
    {
        HttpClient tempHttpClient = new HttpClient();

        HttpResponseMessage resp = await tempHttpClient.GetAsync($"https://www.alphavantage.co/query?function=RSI&symbol=IBM&interval=weekly&time_period=10&series_type=close&apikey={_alphaVantageApiKey}&datatype=csv");
    
        if (resp.IsSuccessStatusCode == false)
        {
            return;
        }

        var readAsStringAsync = resp.Content.ReadAsStringAsync();
        
        var dailyItem = readAsStringAsync.Result.FromCsv<List<AlphaVantageRsiFormat>>();
    }

    //,

    [DataContract]
    private class AlphaVantageRsiFormat
    {
        [DataMember] public DateTime time { get; set; }
        [DataMember] public decimal RSI { get; set; }
    }
#endif

    /*
            symbol,open,high,low,price,volume,latestDay,previousClose,change,changePercent
            LUMN,13.9300,14.0900,13.8820,14.0500,5368898,2021-06-25,13.9500,0.1000,0.7168%"
     */
    [DataContract]
    private class AlphaVantageQuoteFormat
    {
        [DataMember] public string symbol { get; set; }
        [DataMember] public decimal open { get; set; }
        [DataMember] public decimal high { get; set; }
        [DataMember] public decimal low { get; set; }
        [DataMember] public decimal price { get; set; }
        [DataMember] public int volume { get; set; }
        [DataMember] public DateTime latestDay { get; set; }
        [DataMember] public decimal previousClose { get; set; }
        [DataMember] public decimal change { get; set; }
        [DataMember] public string changePercent { get; set; }
    }


    /*
        timestamp,open,high,low,close,volume
        2021-06-25,13.9300,14.0900,13.8820,14.0500,5368898
    */
    [DataContract]
    private class AlphaVantageDailyFormat
    {
        [DataMember] public DateTime timestamp { get; set; }
        [DataMember] public decimal open { get; set; }
        [DataMember] public decimal high { get; set; }
        [DataMember] public decimal low { get; set; }
        [DataMember] public decimal close { get; set; }
        [DataMember] public int volume { get; set; }
    }
}
