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
using System.Text.Json;
using static Pfs.Types.IExtDataProvider;

namespace Pfs.ExtProviders;

public class ExtPolygon : IExtProvider, IExtDataProvider, IExtCurrencyProvider
{
    /* - Only USA markets!, has currency support ex EUR -> US for Latest & History
     * - 5 calls per minute limit, one ticker at time... but no other limits
     * 
     * TRY OUT: 
     * 
     * - Good looking API to get divident history, w same 5 querys a minute limit! Nice!
     *      => Show history, shows next divident.. all nice.. but slow 
     */

    protected readonly IPfsStatus _pfsStatus;

    protected string _error = string.Empty;

    protected string _polygonApiKey = "";

    public ExtPolygon(IPfsStatus pfsStatus)
    {
        _pfsStatus = pfsStatus;
    }

    public string GetLastError() { return _error; }

    public void SetPrivateKey(string key)
    {
        _polygonApiKey = key;
    }

    public int GetLastCreditPrice() { return 0; }

    public bool IsSupport(ProvFunctionId funcId)
    {
        switch (funcId)
        {
            case ProvFunctionId.LatestEod:
                return true;

            case ProvFunctionId.Intraday:
                return false; // w 8 req / min, cant see value for this even works
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
        Log.Warning($"ExtMarketDataPolygon():Limit({limitID}) missing implementation");
        return 1;
    }

    public int CurrencyLimit(IExtCurrencyProvider.CurrencyLimitId limitId)
    {
        switch (limitId)
        {
            case IExtCurrencyProvider.CurrencyLimitId.SupportBatchFetch: return 0; // not supported
        }
        return 0;
    }

    protected DateTime _lastUseTime = DateTime.UtcNow.AddMinutes(-1);

    protected async Task InternalDelayAwait()
    {
        int speedCap = _pfsStatus.GetAppCfg(AppCfgId.PolygonSpeedSecs);

        if (speedCap > 1 && DateTime.UtcNow < _lastUseTime.AddSeconds(speedCap))
        {
            int seconds = (int)(_lastUseTime.AddSeconds(speedCap) - DateTime.UtcNow).TotalSeconds;
            seconds = seconds == 0 ? 1 : seconds > speedCap ? speedCap : seconds;

            await Task.Delay(seconds * 1000);
        }
        _lastUseTime = DateTime.UtcNow;
    }

    public async Task<Dictionary<string, FullEOD>> GetIntradayAsync(MarketId marketId, List<string> tickers)
    {
        _error = "GetIntradayAsync() - Not supported!";
        return null;
    }

    public async Task<Dictionary<string, FullEOD>> GetEodLatestAsync(MarketId marketId, List<string> tickers)
    {
        _error = string.Empty;

        if (string.IsNullOrEmpty(_polygonApiKey) == true)
        {
            _error = "Polygon::GetEodLatestAsync() Missing private access key!";
            return null;
        }

        if (tickers.Count != 1)
        {
            _error = "Failed, has 1 ticker on time limit!";
            Log.Warning("Polygon::GetEodLatestAsync() " + _error);
            return null;
        }

        await InternalDelayAwait();

        try
        {
            HttpClient tempHttpClient = new HttpClient();
            tempHttpClient.Timeout = TimeSpan.FromSeconds(20);

            // Yes, as of 2021-Jul this works OK!
            HttpResponseMessage resp = await tempHttpClient.GetAsync($"https://api.polygon.io/v2/aggs/ticker/{tickers[0]}/prev?adjusted=true&apiKey={_polygonApiKey}");

            if (resp.IsSuccessStatusCode == false)
            {
                _error = $"Polygon failed: {resp.StatusCode} for [[{tickers[0]}]]";
                Log.Warning("Polygon::GetEodLatestAsync() " + _error);
                return null;
            }

            string content = await resp.Content.ReadAsStringAsync();
            var dailyItem = JsonSerializer.Deserialize<PolygonLatestEodRoot>(content);

            if (dailyItem == null || dailyItem.results == null || dailyItem.results.Count() != 1)
            {
                _error = $"Failed, empty data: {resp.StatusCode} for [[{tickers[0]}]]";
                Log.Warning("Polygon::GetEodLatestAsync() " + _error);
                return null;
            }

            // All seams to be enough well so lets convert data to PFS format

            Dictionary<string, FullEOD> ret = new Dictionary<string, FullEOD>();

            ret[tickers[0]] = new FullEOD()
            {
                Date = DateOnly.FromDateTime(DateTime.UnixEpoch.AddMilliseconds(dailyItem.results[0].t)),
                Close = dailyItem.results[0].c,
                High = dailyItem.results[0].h,
                Low = dailyItem.results[0].l,
                Open = dailyItem.results[0].o,
                PrevClose = -1,
                Volume = (int)(dailyItem.results[0].v),
            };

            return ret;
        }
        catch ( Exception e )
        {
            _error = $"Polygon connection exception {e.Message} for [[{tickers[0]}]]";
            Log.Warning("Polygon::GetEodLatestAsync() " + _error);
        }
        return null;
    }

    public async Task<Dictionary<string, List<FullEOD>>> GetEodHistoryAsync(MarketId marketId, List<string> tickers, DateTime startDay, DateTime endDay)
    {
        _error = string.Empty;

        if (string.IsNullOrEmpty(_polygonApiKey) == true)
        {
            _error = "Polygon::GetEodHistoryAsync() Missing private access key!";
            return null;
        }

        if (tickers.Count != 1)
        {
            _error = "Failed, has 1 ticker on time limit!";
            Log.Warning("Polygon::GetEodLatestAsync() " + _error);
            return null;
        }

        string start = startDay.ToString("yyyy-MM-dd");
        string end = endDay.ToString("yyyy-MM-dd");

        await InternalDelayAwait();

        try
        {
            HttpClient tempHttpClient = new HttpClient();
            HttpResponseMessage resp = await tempHttpClient.GetAsync($"https://api.polygon.io/v2/aggs/ticker/{tickers[0]}/range/1/day/{start}/{end}?adjusted=true&sort=asc&limit=1000&apiKey={_polygonApiKey}");

            if (resp.IsSuccessStatusCode == false)
            {
                _error = $"Failed: {resp.StatusCode} for [[{tickers[0]}]]";
                Log.Warning("Polygon::GetEodHistoryAsync() " + _error);
                return null;
            }

            string content = await resp.Content.ReadAsStringAsync();
            var polygonResp = JsonSerializer.Deserialize<PolygonHistoryEodRoot>(content);

            if (polygonResp == null || polygonResp.results == null || polygonResp.results.Count() == 0)
            {
                _error = $"Failed, empty data: {resp.StatusCode} for [[{tickers[0]}]]";
                Log.Warning("Polygon::GetEodHistoryAsync() " + _error);
                return null;
            }

            Dictionary<string, List<FullEOD>> ret = new Dictionary<string, List<FullEOD>>();

            List<FullEOD> ext = polygonResp.results.ConvertAll(s => new FullEOD()
            {
                Date = DateOnly.FromDateTime(DateTime.UnixEpoch.AddMilliseconds(s.t)),
                Close = s.c,
                High = s.h,
                Low = s.l,
                Open = s.o,
                PrevClose = -1,
                Volume = (int)(s.v),
            });

            //ext.Reverse();
            ret.Add(tickers[0], ext);

            return ret;
        }
        catch (Exception e)
        {
            _error = $"Failed, connection exception {e.Message} for [[{tickers[0]}]]";
            Log.Warning("Polygon::GetEodHistoryAsync() " + _error);
        }
        return null;
    }

    public async Task<(DateTime UTC, Dictionary<CurrencyId, decimal> rates)> 
        GetCurrencyLatestAsync(CurrencyId toCurrency, CurrencyId fromCurrency = CurrencyId.Unknown)
    {
        _error = string.Empty;

        if (string.IsNullOrEmpty(_polygonApiKey) == true)
        {
            _error = "Polygon::GetCurrencyLatestAsync() Missing private access key!";
            return (UTC: DateTime.MinValue, rates: null);
        }

        if (fromCurrency == CurrencyId.Unknown)
            return (UTC: DateTime.MinValue, rates: null);

        await InternalDelayAwait();

        string combo = string.Format("{0}{1}", fromCurrency.ToString(), toCurrency.ToString());

        try
        {
            HttpClient tempHttpClient = new HttpClient();
            tempHttpClient.Timeout = TimeSpan.FromSeconds(20);

            HttpResponseMessage resp = await tempHttpClient.GetAsync($"https://api.polygon.io/v2/aggs/ticker/C:{combo}/prev?adjusted=true&apiKey={_polygonApiKey}");

            if (resp.IsSuccessStatusCode == false)
            {
                _error = $"Failed: {resp.StatusCode} for [[{combo}]]";
                Log.Warning("Polygon::GetCurrencyLatestAsync() " + _error);
                return (UTC: DateTime.MinValue, rates: null);
            }

            string content = await resp.Content.ReadAsStringAsync();
            var polygonCurrency = JsonSerializer.Deserialize<PolygonLatestCurrencyRoot>(content);

            if (polygonCurrency == null || polygonCurrency.results == null || polygonCurrency.results.Count() != 1)
            {
                _error = $"Failed, empty data: {resp.StatusCode} for [[{combo}]]";
                Log.Warning("Polygon::GetCurrencyLatestAsync() " + _error);
                return (UTC: DateTime.MinValue, rates: null);
            }

            DateTime dateUTC = DateTime.UnixEpoch.AddMilliseconds(polygonCurrency.results[0].t);

            // All seams to be enough well so lets convert data to PFS format
            Dictionary<CurrencyId, decimal> ret = new();
            ret.Add(fromCurrency, polygonCurrency.results[0].c);

            return (UTC: dateUTC, rates: ret);
        }
        catch (Exception e)
        {
            _error = $"Connection exception {e.Message} for [[{combo}]]";
            Log.Warning("Polygon::GetCurrencyLatestAsync() " + _error);
        }
        return (UTC: DateTime.MinValue, rates: null);
    }

    public async Task<Dictionary<CurrencyId, decimal>> 
        GetCurrencyHistoryAsync(DateOnly date, CurrencyId toCurrency, CurrencyId fromCurrency = CurrencyId.Unknown)
    {
        _error = string.Empty;

        if (string.IsNullOrEmpty(_polygonApiKey) == true)
        {
            _error = "Polygon::GetCurrencyHistoryAsync() Missing private access key!";
            return null;
        }

        if (fromCurrency == CurrencyId.Unknown)
            return null;

        await InternalDelayAwait();

        string combo = string.Format("{0}{1}", fromCurrency.ToString(), toCurrency.ToString());

        string start = date.ToString("yyyy-MM-dd");     // Note! This could support range also, but atm doesnt have use for it
        string end = date.ToString("yyyy-MM-dd");       //       if later need then implement as new API GetCurrencyRangeAsync

        try
        {
            HttpClient tempHttpClient = new HttpClient();
            tempHttpClient.Timeout = TimeSpan.FromSeconds(20);

            HttpResponseMessage resp = await tempHttpClient.GetAsync($"https://api.polygon.io/v2/aggs/ticker/C:{combo}/range/1/day/{start}/{end}?adjusted=true&apiKey={_polygonApiKey}");

            if (resp.IsSuccessStatusCode == false)
            {
                _error = $"Failed: {resp.StatusCode} for [[{combo}]]";
                Log.Warning("Polygon::GetCurrencyHistoryAsync() " + _error);
                return null;
            }

            string content = await resp.Content.ReadAsStringAsync();
            var polygonCurrency = JsonSerializer.Deserialize<PolygonLatestCurrencyRoot>(content);

            if (polygonCurrency == null || polygonCurrency.results == null || polygonCurrency.results.Count() != 1)
            {
                _error = $"Failed, empty/invalid data: {resp.StatusCode} for [[{combo}]]";
                Log.Warning("Polygon::GetCurrencyHistoryAsync() " + _error);
                return null;
            }

            Dictionary<CurrencyId, decimal> ret = new();
            ret.Add(fromCurrency, polygonCurrency.results[0].c);

            //return polygonCurrency.results.ConvertAll(c => new Tuple<DateTime, decimal>(DateTime.UnixEpoch.AddMilliseconds(c.t), c.c));
            return ret;
        }
        catch (Exception e)
        {
            _error = $"Connection exception {e.Message} for [[{combo}]]";
            Log.Warning("Polygon::GetCurrencyHistoryAsync() " + _error);
        }
        return null;
    }

    public class PolygonLatestEodResult
    {
        public string T { get; set; }
        public decimal v { get; set; }
        public decimal vw { get; set; }
        public decimal o { get; set; }
        public decimal c { get; set; }
        public decimal h { get; set; }
        public decimal l { get; set; }
        public long t { get; set; }
        public int n { get; set; }
    }

    public class PolygonLatestEodRoot
    {
        public string ticker { get; set; }
        public int queryCount { get; set; }
        public int resultsCount { get; set; }
        public bool adjusted { get; set; }
        public List<PolygonLatestEodResult> results { get; set; }
        public string status { get; set; }
        public string request_id { get; set; }
        public int count { get; set; }
    }

    public class PolygonHistoryEodResult
    {
        public decimal v { get; set; }
        public decimal vw { get; set; }
        public decimal o { get; set; }
        public decimal c { get; set; }
        public decimal h { get; set; }
        public decimal l { get; set; }
        public long t { get; set; }
        public int n { get; set; }
    }

    public class PolygonHistoryEodRoot
    {
        public string ticker { get; set; }
        public int queryCount { get; set; }
        public int resultsCount { get; set; }
        public bool adjusted { get; set; }
        public List<PolygonHistoryEodResult> results { get; set; }
        public string status { get; set; }
        public string request_id { get; set; }
        public int count { get; set; }
    }

    public class PolygonLatestCurrencyResult
    {
        public string T { get; set; }
        public int v { get; set; }
        public decimal o { get; set; }
        public decimal c { get; set; }
        public decimal h { get; set; }
        public decimal l { get; set; }
        public long t { get; set; }
        public int n { get; set; }
    }

    public class PolygonLatestCurrencyRoot
    {
        public string ticker { get; set; }
        public int queryCount { get; set; }
        public int resultsCount { get; set; }
        public bool adjusted { get; set; }
        public List<PolygonLatestCurrencyResult> results { get; set; }
        public string status { get; set; }
        public string request_id { get; set; }
        public int count { get; set; }
    }

}

