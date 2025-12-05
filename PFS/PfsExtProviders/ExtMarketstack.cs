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

// Marketstack: https://marketstack.com/documentation
public class ExtMarketstack : IExtProvider, IExtDataProvider
{
    protected readonly IPfsStatus _pfsStatus;

    protected string _error = string.Empty;

    protected string _marketstackApiKey = "";

    public ExtMarketstack(IPfsStatus pfsStatus)
    {
        _pfsStatus = pfsStatus;
    }

    public string GetLastError() { return _error; }

    public void SetPrivateKey(string key)
    {
        _marketstackApiKey = key;
    }

    public int GetLastCreditPrice() { return 0; }

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
        switch (marketId)   // Warning! See 'GetMIC' below, thats needs hardcoding atm
        {
            case MarketId.NASDAQ:
            case MarketId.NYSE:
            case MarketId.AMEX:
            case MarketId.TSX:
            case MarketId.TSXV:
            case MarketId.OMXH:
            case MarketId.OMX:
            case MarketId.XETRA:
            case MarketId.LSE:
                return true;
        }
        return false;
    }

    const int _maxTickers = 20; // As of 2021-Nov-9th, did try 100->50->20.. and as this fails full patch if one stock fails inside it just 
                                // wastes lot of credits most time.. so 20 it is now, speed is not benefit if half goes to retry...

    public int GetBatchSizeLimit(ProvFunctionId limitID)
    {
        switch (limitID)
        {
            case IExtDataProvider.ProvFunctionId.LatestEod:
            case IExtDataProvider.ProvFunctionId.HistoryEod: return _maxTickers;

            case IExtDataProvider.ProvFunctionId.Intraday: return 1;           // Supposed to have API for 100, but it didnt work.. so 1-by-1 it is :/
        }
        Log.Warning($"ExtMarketstack():Limit({limitID}) missing implementation");
        return 1;
    }

    /* Note! 
        * - Cant use Free account on WASM, as only http thats not allowed w https wasm.. so minimum 9$ account required
        *   (To test with Free account on Developer Studio a servers works ok, but WASM only if application NOT on HTTPS,
        *    so need to change launchSettings.json to not have sslPort defined)
        * 
        * - 
        *    
        *    
        *    
        */

    public async Task<Dictionary<string, FullEOD>> GetEodLatestAsync(MarketId marketId, List<string> tickers)
    {
        _error = string.Empty;

        if (string.IsNullOrEmpty(_marketstackApiKey) == true)
        {
            _error = "Marketstack:GetEodLatestAsync() Missing private access key!";
            return null;
        }

        int amountOfReqTickers = tickers.Count();

        if (tickers.Count() > _maxTickers)
        {
            _error = "Failed, requesting too many tickers!";
            Log.Warning("Marketstack:GetEodLatestAsync() " + _error);
            return null;
        }

        string joinedMsTickers = JoinPfsTickers(marketId, tickers);

        try
        {
            HttpClient tempHttpClient = new HttpClient();

            //string start = "2025-11-01";
            //string end = "2025-11-16";
            //string temp = $"https://api.marketstack.com/v2/eod?access_key={_marketstackApiKey}&date_from={start}&date_to={end}&exchange={GetMIC(marketId)}&symbols={joinedTickers}";

            //string temp = $"https://api.marketstack.com/v2/eod/2025-11-13?access_key={_marketstackApiKey}&exchange={GetMIC(marketId)}&symbols={joinedMsTickers}";

            HttpResponseMessage resp = await tempHttpClient.GetAsync($"https://api.marketstack.com/v2/eod/latest?access_key={_marketstackApiKey}&exchange={GetMIC(marketId)}&symbols={joinedMsTickers}");
            
            if (resp.StatusCode != System.Net.HttpStatusCode.OK)
            {
                _error = string.Format("Failed: {0} for [[{1}]]", resp.StatusCode.ToString(), joinedMsTickers);
                Log.Warning("Marketstack:GetEodLatestAsync() " + _error);
                return null;
            }

            string content = await resp.Content.ReadAsStringAsync();
            var dailyItems = JsonSerializer.Deserialize<MarketstackPeriod>(content);

            if (dailyItems == null || dailyItems.data == null || dailyItems.data.Count() == 0)
            {
                _error = string.Format("Failed, Empty data: {0} for [[{1}]]", resp.StatusCode.ToString(), joinedMsTickers);
                Log.Warning("Marketstack:GetEodLatestAsync() " + _error);
                return null;
            }
            else if (dailyItems.data.Count() < amountOfReqTickers)
            {
                _error = string.Format("Warning, requested {0} got just {1} for [[{2}]]", amountOfReqTickers, dailyItems.data.Count(), joinedMsTickers);
                Log.Warning("Marketstack:GetEodLatestAsync() " + _error);

                // This is just warning, still got data so going to go processing it...
            }

            // All seams to be enough well so lets convert data to PFS format

            Dictionary<string, FullEOD> ret = new Dictionary<string, FullEOD>();

            foreach (var item in dailyItems.data)
            {
                ret[TrimToPfsTicker(marketId, item.symbol)] = new FullEOD()
                {
                    Date = DateOnly.ParseExact(item.date.Substring(0,10), "yyyy-MM-dd"), // 2021-12-06T00:00:00+0000
                    Close = item.close,
                    High = item.high,
                    Low = item.low,
                    Open = item.open,
                    PrevClose = -1,
                    Volume = (int)(item.volume),
                };
            }

            return ret;
        }
        catch (Exception e)
        {
            _error = string.Format("Failed, connection exception {0} for [[{1}]]", e.Message, joinedMsTickers);
            Log.Warning("Marketstack:GetEodLatestAsync() " + _error);
        }
        return null;
    }

    public async Task<Dictionary<string, List<FullEOD>>> GetEodHistoryAsync(MarketId marketId, List<string> tickers, DateTime startDay, DateTime endDay)
    {
        _error = string.Empty;

        if (string.IsNullOrEmpty(_marketstackApiKey) == true)
        {
            _error = "Marketstack:GetEodHistoryAsync() Missing private access key!";
            return null;
        }

        if (tickers.Count() > _maxTickers)
        {
            _error = "Failed, requesting too many tickers!";
            Log.Warning("Marketstack:GetEodHistoryAsync() " + _error);
            return null;
        }

        string joinedTickers = JoinPfsTickers(marketId, tickers);

        string start = startDay.ToString("yyyy-MM-dd");
        string end = endDay.ToString("yyyy-MM-dd");

        try
        {
            HttpClient tempHttpClient = new HttpClient();

            // Using date_from -> date_to seams to work also, go with this one atm!
            HttpResponseMessage resp = await tempHttpClient.GetAsync($"https://api.marketstack.com/v1/eod?access_key={_marketstackApiKey}&date_from={start}&date_to={end}&exchange={GetMIC(marketId)}&symbols={joinedTickers}");

            // Works also with /eod/2021-08-01?... 
            // HttpResponseMessage resp = await tempHttpClient.GetAsync($"https://api.marketstack.com/v1/eod/{start}?access_key={_marketstackApiKey}&exchange={marketMeta.MIC}&symbols={joinedTickers}");

            if (resp.IsSuccessStatusCode == false)
            {
                _error = string.Format("Failed: {0} for [[{1}]]", resp.StatusCode.ToString(), joinedTickers);
                Log.Warning("Marketstack:GetEodHistoryAsync() " + _error);
                return null;
            }

            string content = await resp.Content.ReadAsStringAsync();
            var stockRecords = JsonSerializer.Deserialize<MarketstackPeriod>(content);

            if (stockRecords == null || stockRecords.data == null || stockRecords.data.Count() == 0)
            {
                _error = string.Format("Failed, empty data: {0} for [[{1}]]", resp.StatusCode.ToString(), joinedTickers);
                Log.Warning("Marketstack:GetEodHistoryAsync() " + _error);
                return null;
            }

            Dictionary<string, List<FullEOD>> ret = new Dictionary<string, List<FullEOD>>();

            int receivedDataAmount = 0;
            foreach (string ticker in tickers)
            {
                string msTicker = ExpandToMsTicker(marketId, ticker);

                List<MarketstackPeriodEOD> partial = stockRecords.data.Where(s => s.symbol == msTicker).ToList();

                if (partial.Count() == 0)
                {
                    // this one didnt receive any data
                    Log.Warning("Marketstack:GetEodHistoryAsync() no data for {0}", ticker);
                    continue;
                }

                List<FullEOD> ext = partial.ConvertAll(s => new FullEOD()
                {
                    Date = DateOnly.ParseExact(s.date.Substring(0, 10), "yyyy-MM-dd"), // 2021-12-06T00:00:00+0000
                    Close = s.close,
                    High = s.high,
                    Low = s.low,
                    Open = s.open,
                    PrevClose = -1,
                    Volume = (int)(s.volume),
                });

                ext.Reverse();
                ret.Add(TrimToPfsTicker(marketId, ticker), ext);

                receivedDataAmount++;
            }

            if (receivedDataAmount < tickers.Count())
            {
                _error = string.Format("Warning, requested {0} got {1} for [[{2}]]", tickers.Count(), receivedDataAmount, joinedTickers);
                Log.Warning("Marketstack:GetEodHistoryAsync() " + _error);
            }

            return ret;
        }
        catch (Exception e)
        {
            _error = string.Format("Failed, connection exception {0} for [[{1}]]", e.Message, joinedTickers);
            Log.Warning("Marketstack:GetEodHistoryAsync() " + _error);
        }
        return null;
    }

    public async Task<Dictionary<string, FullEOD>> GetIntradayAsync(MarketId marketId, List<string> tickers)
    {
        // Note! Actually works for intraday, but need to call each ticker separately, and eats 1 point per ticker.. so credits go fast!!
        //       Even w 9$ 10000 credits would need to be carefull not to consume too fastly a monthly plan! Bah Bah Bah...

        _error = string.Empty;

        if (string.IsNullOrEmpty(_marketstackApiKey) == true)
        {
            _error = "Marketstack:GetIntradayAsync() Missing private access key!";
            return null;
        }

        if (tickers.Count() != 1)
        {
            _error = "Failed, requesting too many tickers!";
            Log.Warning("Marketstack:GetIntradayAsync() " + _error);
            return null;
        }

        string ticker = ExpandToMsTicker(marketId, tickers[0]);

        try
        {
            HttpClient tempHttpClient = new HttpClient(); // As of 2021-Nov-5th, go with this as fetching multiple doesnt seam to work w intraday/latest?...
            HttpResponseMessage resp = await tempHttpClient.GetAsync($"https://api.marketstack.com/v1/tickers/{ticker}/intraday/latest?access_key={_marketstackApiKey}&exchange={GetMIC(marketId)}");

            // (2021-Nov) This atm hardcoded to AUY,VST.. they work separately but not together.. so hmm.. try later again...LUMN didnt wanna work at all...
            //HttpResponseMessage resp = await tempHttpClient.GetAsync($"https://api.marketstack.com/v1/intraday/latest?access_key={_marketstackApiKey}&symbols=AUY,VST&exchange={marketMeta.MIC}");

            if (resp.IsSuccessStatusCode == false)
            {
                _error = string.Format("Failed: {0} for [[{1}]]", resp.StatusCode.ToString(), ticker);
                Log.Warning("Marketstack:GetIntradayAsync() " + _error);
                return null;
            }

            string content = await resp.Content.ReadAsStringAsync();
            var intradayData = JsonSerializer.Deserialize<MarketstackIntraday>(content);

            if (intradayData == null || intradayData.last == 0)
            {
                _error = string.Format("Failed, Empty data: {0} for [[{1}]]", resp.StatusCode.ToString(), ticker);
                Log.Warning("Marketstack:GetIntradayAsync() " + _error);
                return null;
            }

            // All seams to be enough well so lets convert data to PFS format

            Dictionary<string, FullEOD> ret = new Dictionary<string, FullEOD>();

            ret[tickers[0]] = new FullEOD()
            {
                Date = DateOnly.ParseExact(intradayData.date.Substring(0, 16), "yyyy-MM-ddTHH:mm"), // 2021-12-06T20:00:00+0000
                Close = intradayData.last,
                Open = intradayData.open,
                High = intradayData.high,
                Low = intradayData.low,
                Volume = (int)intradayData.volume,
                PrevClose = -1,
            };

            return ret;
        }
        catch (Exception e)
        {
            _error = string.Format("Failed, connection exception {0} for [[{1}]]", e.Message, ticker);
            Log.Warning("Marketstack:GetIntradayAsync() " + _error);
        }
        return null;
    }

    //
    // Additional functionalities provided those are not part of generic Interface
    //
#if false
    public async Task<List<DividentPayment>> GetDividentHistoryAsync(MarketMeta marketMeta, List<string> tickers, DateTime fromDate)
    {
        _error = string.Empty;

        if (string.IsNullOrEmpty(_marketstackApiKey) == true)
        {
            _error = "Marketstack:GetDividentHistoryAsync() Missing private access key!";
            return null;
        }

        if (tickers.Count() != 1)
        {
            _error = "Failed, requesting too many tickers!";
            Log.Warning("Marketstack:GetDividentHistoryAsync() " + _error);
            return null;
        }

        string ticker = ExpandToMsTicker(marketMeta, tickers[0]);

        try
        {
            string from = fromDate.ToString("yyyy-MM-dd");

            HttpClient tempHttpClient = new HttpClient(); // Note! Keep this as a http so works w development or premium key (as only used atm backends)!
            HttpResponseMessage resp = await tempHttpClient.GetAsync($"http://api.marketstack.com/v1/tickers/{ticker}/dividends?date_from={from}&limit=1000&access_key={_marketstackApiKey}");

            if (resp.IsSuccessStatusCode == false)
            {
                string errorMsg = string.Format("GetDividentHistoryAsync() Failed: {0}", resp.StatusCode.ToString());
                Log.Warning(errorMsg);
                return new();
            }

            string content = await resp.Content.ReadAsStringAsync();
            var recvMeta = JsonSerializer.Deserialize<MarketstackDivRoot>(content);

            List<DividentPayment> ret = new();

            if (recvMeta != null && recvMeta.data != null)
            {
                foreach (MarketstackDivDatum data in recvMeta.data)
                {
                    ret.Add(new()
                    {
                        ExDivDate = DateTime.ParseExact(data.date, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                        PaymentDate = DateTime.ParseExact(data.date, "yyyy-MM-dd", CultureInfo.InvariantCulture), // !!!MISSING!!! :/
                        PayPerUnit = (decimal)data.dividend,
                    });
                }
            }
            ret.Reverse(); // Oldest first, latest divident last.. 
            return ret;
        }
        catch (Exception e)
        {
            _error = string.Format("Failed, connection exception {0} for [[{1}]]", e.Message, ticker);
            Log.Warning("Marketstack:GetDividentHistoryAsync() " + _error);
        }
        return null;
    }
#endif
#if false
    public async Task<List<CompanyMeta>> GetMarketCompaniesAsync(MarketMeta marketMeta)
    {
        _error = string.Empty;

        if (string.IsNullOrEmpty(_marketstackApiKey) == true)
        {
            _error = "Marketstack:GetMarketCompaniesAsync() Missing private access key!";
            return null;
        }

        string mic = GetMIC(marketMeta);

        try
        {
            List<CompanyMeta> marketsCompanyMeta = new();

            int doubleCheck = 0;

            // Warning! Marketstack seams to go panic mode if giving unknown MIC, like XETR and returns ALL tickers from ALL markets.. about 200,000 total...

            for (int p = 0; p < 100; p++) // Loop value is here just to limit forever spins... so 100 means 100,000 maximum stocks per single market
            {
                HttpClient tempHttpClient = new HttpClient(); // Note! Keep this as a HTTP now, as not used by WASM
                HttpResponseMessage resp = await tempHttpClient.GetAsync($"http://api.marketstack.com/v1/exchanges/{mic}/tickers?access_key={_marketstackApiKey}&limit=1000&offset={p * 1000}");

                if (resp.IsSuccessStatusCode == false)
                {
                    _error = string.Format("Marketstack:GetMarketCompaniesAsync({0}) Failed: {1}", mic, resp.StatusCode.ToString());
                    Log.Warning(_error);
                    return null;
                }

                string content = await resp.Content.ReadAsStringAsync();
                var recvMeta = JsonSerializer.Deserialize<MarketstackMetaRoot>(content);

                foreach (MarketstackMetaTicker msMeta in recvMeta.data.tickers)
                {
                    if (string.IsNullOrWhiteSpace(msMeta.symbol) == true || string.IsNullOrWhiteSpace(msMeta.name) == true)
                        // Seams to have sometimes.. AMEX/XASE did have atleast...
                        continue;

                    CompanyMeta companyMeta = new()
                    {
                        Ticker = msMeta.symbol.Replace("." + mic, ""),
                        CompanyName = msMeta.name,
                    };

                    marketsCompanyMeta.Add(companyMeta);
                }

                if (recvMeta.pagination.offset + recvMeta.pagination.count == recvMeta.pagination.total)
                    break;

                doubleCheck += 1000;

                if (doubleCheck > recvMeta.pagination.total)
                    // Just in case backup checker, as dont wanna do super duper many request for change of API
                    break;
            }

            return marketsCompanyMeta;
        }
        catch (Exception e)
        {
            _error = string.Format("Failed, connection exception {0} for [[{1}]]", e.Message, mic);
            Log.Warning("Marketstack:GetMarketCompaniesAsync() " + _error);
        }
        return null;
    }
#endif
    //
    // Even that can pass in a Market's MIC value, doesnt actually work without ticker being set for TICKER.MIC for XTSE etc to some markets
    // 

    static public string TrimToPfsTicker(MarketId marketId, string marketstackTicker)
    {
        if (ExpandRequired(marketId) == true )
            return marketstackTicker.Replace("." + GetMarketForSymbol(marketId), ""); // Example XHEL needs ticker on UPM.XHEL format

        return marketstackTicker;
    }

    static public string ExpandToMsTicker(MarketId marketId, string pfsTicker)
    {
        if (ExpandRequired(marketId) == true)
            return string.Format("{0}.{1}", pfsTicker, GetMarketForSymbol(marketId)); // US markets would work ok without, but example XHEL NOT!

        return pfsTicker;
    }

    // 2025-Dec: Somewhere on way they changed way symbols are done to more exotic markets to not use MIC, 
    // but instead shorter code w ticker. So when example used to be BATS.XLON its now just BATS.L.
    
    static public string GetMarketForSymbol(MarketId marketId)
    {
        switch (marketId)
        {
            //case MarketId.OMXH:
            //    return "??";
            //case MarketId.OMX:
            //    return "??";

            case MarketId.LSE:
                return "L";
        }

        // Sadly absolute no idea where going to find these new 'L's as none of their apis seams using them
        // and for sure TSX / XETRA at least works w MIC
        return GetMIC(marketId);
    }

    static public string GetMIC(MarketId marketId)
    {
        switch ( marketId )
        {
            case MarketId.NASDAQ:
                return "XNAS";
            case MarketId.NYSE:
                return "XNYS";
            case MarketId.AMEX:
                return "XNYS";
            case MarketId.TSX:
                return "XTSE";
            case MarketId.OMXH:
                return "XHEL";
            case MarketId.OMX:
                return "XSTO";
            case MarketId.XETRA:
                return "XETRA";
            case MarketId.LSE:
                return "XLON";
            case MarketId.TSXV:
                return "XTSX";
        }
        return null;
    }

    static public string JoinPfsTickers(MarketId marketId, List<string> pfsTickers)
    {
        return string.Join(',', pfsTickers.ConvertAll<string>(s => ExpandToMsTicker(marketId, s)));
    }

    static public bool ExpandRequired(MarketId marketId)
    {
        switch (marketId)
        {
            case MarketId.NASDAQ:
            case MarketId.NYSE:
            case MarketId.AMEX:             // Not sure AMEX yet
                // These seam NOT liking to do expansion... rest of markets seams to REQUIRE it... 
                return false;
        }
        // Assuming everyone else needs this expansion
        return true;
    }

    // !!!REMEMBER!!! JSON!!! https://json2csharp.com/json-to-csharp

    public class MarketstackIntraday
    {
        public decimal open { get; set; }
        public decimal high { get; set; }
        public decimal low { get; set; }
        public decimal last { get; set; }
        public decimal close { get; set; }
        public decimal volume { get; set; }
        public string date { get; set; }
        public string symbol { get; set; }
        public string exchange { get; set; }
    }

    public class MsPeriodPagination
    {
        public int limit { get; set; }
        public int offset { get; set; }
        public int count { get; set; }
        public int total { get; set; }
    }

    public class MarketstackPeriodEOD
    {
        public decimal open { get; set; }
        public decimal high { get; set; }
        public decimal low { get; set; }
        public decimal close { get; set; }
        public decimal volume { get; set; }
        public decimal? adj_high { get; set; }
        public decimal? adj_low { get; set; }
        public decimal adj_close { get; set; }
        public decimal? adj_open { get; set; }
        public decimal? adj_volume { get; set; }
        public decimal split_factor { get; set; }
        public string symbol { get; set; }
        public string exchange { get; set; }
        public string date { get; set; }            // Used to be DateTime but JsonSerializer.Deserialize didnt work w: 2021-12-06T00:00:00+0000
    }

    public class MarketstackPeriod
    {
        public MsPeriodPagination           pagination { get; set; }
        public List<MarketstackPeriodEOD>   data { get; set; }
    }

    // DIVIDENT

    public class MarketstackDivPagination
    {
        public int limit { get; set; }
        public int offset { get; set; }
        public int count { get; set; }
        public int total { get; set; }
    }

    public class MarketstackDivDatum
    {
        public string date { get; set; }
        public double dividend { get; set; }
        public string symbol { get; set; }
    }

    public class MarketstackDivRoot
    {
        public MarketstackDivPagination pagination { get; set; }
        public List<MarketstackDivDatum> data { get; set; }
    }

    // META


    protected class MarketstackMetaPagination
    {
        public int limit { get; set; }
        public int offset { get; set; }
        public int count { get; set; }
        public int total { get; set; }
    }

    protected class MarketstackMetaTicker
    {
        public string name { get; set; }
        public string symbol { get; set; }
        public bool has_intraday { get; set; }
        public bool has_eod { get; set; }
    }

    protected class MarketstackMetaData
    {
        public string name { get; set; }
        public string acronym { get; set; }
        public string mic { get; set; }
        public string country { get; set; }
        public string city { get; set; }
        public string website { get; set; }
        public List<MarketstackMetaTicker> tickers { get; set; }
    }

    protected class MarketstackMetaRoot
    {
        public MarketstackMetaPagination pagination { get; set; }
        public MarketstackMetaData data { get; set; }
    }
}
