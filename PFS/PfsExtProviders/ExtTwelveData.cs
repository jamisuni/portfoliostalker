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

using Serilog;              // Nuget: Serilog
using CsvHelper;
using CsvHelper.Configuration;

using Pfs.Types;
using System.Text.Json;
using System.Text.Json.Serialization;
using static Pfs.Types.IExtDataProvider;

namespace Pfs.ExtProviders;

public class ExtTwelveData : IExtProvider, IExtDataProvider, IExtCurrencyProvider, IExtMetaProvider         // Would love to try more, very potential, but EOD is still not trustable!
{
    /* Initial testing on 2022-Jun, as potential 99$ commercial w all markets. 
     * Potentially ok looking replacement for MS if it fails, but sure 99$ is lot more than 9$. 
     * Then for personal use has 29U$ plan that definedly worth of try if can pass EOD issues, except no Helsinki.
     * 
     * 2022-Jul-8th: Did give this goodie amount of time, they acknowledged error with closing valuations and say looking to 
     * fix it. May even have fixed already. Did also test batch fetch and it works ok looking. Problem now is even 2 hours 
     * of after close many stocks 'time_series' 'previous_close=true'to get latest returns zero volume, for 75% of stocks,
     * including MSFT etc. This maybe something to do with post-market been still open and causing problems
     * 
     * 
     * => STILL HAVENT FOUND TRUSTABLE WAY TO GET FULL EOD VALUATIONS W ALL DATA WO WORRYING THEY GET MESSED W POST MARKET!!!
     * 
     * 
     * Functionalities:
     * 
     * - History seams work all ok on server side w: /time_series?symbol=T:NYSE&start_date=yyyy-MM-dd&end_date=yyyy-MM-dd&interval=1day&apikey=secret
     *      => Could maybe even get batch fetch w JSON, but atm PFS doesnt use it so didnt test
     * 
     * - Has ''End of Day Price ala /eod'' but cant use that one as it only returns closing valuation (even that seams to be off compared google price)
     * 
     * - Batch, works, but w free account its still limited 7 (or 8) tickers per minute so zero benefits using it yet on early testing
     * 
     * - Intraday works nicely, but w speed limit caps its usefullness on Free -accounts
     * 
     * - /exchange_rate provides required data for latest/history things, allowing this to be used as currency provider for Free/Local accounts
     * 
     * - Data availability is excelent, looks like would be faster availability than anyone else. Potentially w premium could do intradays also!
     * 
     * Problems:
     * 
     * - Cant find any trustable way to figure out if given value from /time_series or /quote is actually true final end of day close valuation,
     *   even fcking /eod returns wrong value using it after market closing... I mean error is just few cent errors but still its wrong!
     *      => No point ordering this provider atm, as most important feature a 'end of day prices' is not giving correct values :/
     *  
     *  Next w payed account: (postponed until 'eod' issue is fixed, and can give proper testing for 'eod' usage on server side)
     *  
     *  - /dividends/last should fix issues w sleepy Marketstack
     *  
     *  Ignore:
     *  
     *  - /eod ala ''End of Day Price'' doesnt have all data, just use other APIs
     *  
     *  Later:
     *  
     *  - /quote would give also "fifty_two_week": valuations, allowing to create visual where we currently on price view
     *  
     *  - Stocks List && ETF List, to provide META      (6MB ALL Stocks: https://api.twelvedata.com/stocks?format=CSV)
     *      => Per Market: https://api.twelvedata.com/stocks?exchange=OMXH 
     *  
     *  - Market State, to show status of markets
     *  
     *  - Technical Indicators, has a lot lot of them here..  could be lazy and pull RSI/MFI/MACD from here w payed account.
     *      => At least test this out someday if nothing else to compare/verify my own calculations 
     * 
     */

    protected readonly IPfsStatus _pfsStatus;

    protected string _error = string.Empty;

    protected string _twelveDataApiKey = "";

    public ExtTwelveData(IPfsStatus pfsStatus)
    {
        _pfsStatus = pfsStatus;
    }

    public string GetLastError() { return _error; }

    public void SetPrivateKey(string key)
    {
        _twelveDataApiKey = key;
    }

    public int GetLastCreditPrice() { return 1; }

    protected DateTime _lastUseTime = DateTime.UtcNow;

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
        switch ( marketId )
        {
            case MarketId.NASDAQ:   // Freebie limited to these
            case MarketId.NYSE:

#if false
            case MarketID.AMEX:     // 29U$ gets London & German etc some majors
            case MarketId.TSX:
            case MarketID.XETRA:

            case MarketID.OMX:      // 99U$ gets these 
            case MarketID.OMXH:
#endif
                return true;
        }
        return false;
    }

    // Note! w Free account, its limited to 8 request per minute, and batching up doesnt help. So if trying to batch things up
    //       a total speed stays same as each request spends more of those minute slots. So for free account, has to keep this
    //       as a no batching :/ 
    const int _maxTickers = 1; // Can batch w free a '7' stocks on time.. but that eats minute limit ending, so no benefit! => batch disabled
    
    public int GetBatchSizeLimit(IExtDataProvider.ProvFunctionId limitID)
    {
        switch (limitID)
        {
            case IExtDataProvider.ProvFunctionId.LatestEod: return _maxTickers;
            case IExtDataProvider.ProvFunctionId.HistoryEod: return 1;
            case IExtDataProvider.ProvFunctionId.Intraday: return 1;
        }
        Log.Warning($"ExtMarketDataTwelveData():Limit({limitID}) missing implementation");
        return 1;
    }

    public int CurrencyLimit(IExtCurrencyProvider.CurrencyLimitId limitId)
    {
        switch (limitId)
        {
            case IExtCurrencyProvider.CurrencyLimitId.SupportBatchFetch: return 0; // As of 2022-Jun, disabled as no real help w free account!
        }
        return 0;
    }

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
        _error = string.Empty;

        if (string.IsNullOrEmpty(_twelveDataApiKey) == true)
        {
            _error = "TWELVE::GetIntradayAsync() Missing private access key!";
            return null;
        }

        int amountOfReqTickers = tickers.Count();

        string twelveJoinedTickers = JoinPfsTickers(marketId, tickers);

        if (string.IsNullOrEmpty(twelveJoinedTickers) == true)
        {
            _error = "Failed, too many tickers!";
            Log.Warning("TWELVE::GetIntradayAsync() " + _error);
            return null;
        }

        await InternalDelayAwait();

        try
        {
            HttpClient tempHttpClient = new HttpClient();

            HttpResponseMessage resp = await tempHttpClient.GetAsync($"https://api.twelvedata.com/time_series?symbol={twelveJoinedTickers}&outputsize=1&interval=1min&apikey={_twelveDataApiKey}&format=JSON");

            if (resp.IsSuccessStatusCode == false)
            {
                _error = string.Format("TwelveD failed: {0} for [[{1}]]", resp.StatusCode.ToString(), tickers[0]);
                Log.Warning("TWELVE::GetIntradayAsync() " + _error);
                return null;
            }

            string content = await resp.Content.ReadAsStringAsync();

            Log.Information($"content=[{content}]");

            if (amountOfReqTickers == 1)
            {                           
                var companyData = JsonSerializer.Deserialize<TwelveTimeSeriesJsonCompany>(content);
                if (companyData == null || companyData.values == null)
                {
                    _error = string.Format("Failed, Empty data: {0} for [[{1}]]", resp.StatusCode.ToString(), twelveJoinedTickers);
                    Log.Warning("TWELVE:GetIntradayAsync() " + _error);
                    return null;
                }

                FullEOD eod = new()
                {
                    //                    DayTime = DateTime.Now, // best that can be done here, but actually pretty accurate                   !!!TODO!!! 2024 - needs at least 'time' there? => Own Intra structure
                    //                    Latest = Decimal.Parse(companyData.values[0].close),
                    High = Decimal.Parse(companyData.values[0].high),
                    Low = Decimal.Parse(companyData.values[0].low),
                    Open = Decimal.Parse(companyData.values[0].open),
                    PrevClose = -1,     // dont start pulling it here from previous if dont have field, let central place to do pulling from prev day
                    Volume = int.Parse(companyData.values[0].volume),
                };

                Dictionary<string, FullEOD> ret = new Dictionary<string, FullEOD>();
                ret.Add(companyData.meta.symbol, eod);

                return ret;
            }
            else                                                // BATCH -request
            {
                // Note! This is specially nasty case as top records come w variable names, so we do first Root conversion w dynamic dictionary
                //       and then second level conversion from there a company-by-company to TwelveTimeSeriesJsonCompany format

                var batchItems = JsonSerializer.Deserialize<TwelveTimeSeriesJsonBatchRoot>(content);
                if (batchItems == null || batchItems.Entries == null || batchItems.Entries.Count() == 0)
                {
                    _error = string.Format("Failed, Empty data: {0} for [[{1}]]", resp.StatusCode.ToString(), twelveJoinedTickers);
                    Log.Warning("TWELVE:GetIntradayAsync(BATCH) " + _error);
                    return null;
                }
                else if (batchItems.Entries.Count() < amountOfReqTickers)
                {
                    _error = string.Format("Warning, requested {0} got just {1} for [[{2}]]", amountOfReqTickers, batchItems.Entries.Count(), twelveJoinedTickers);
                    Log.Warning("TWELVE:GetIntradayAsync(BATCH) " + _error);

                    // This is just warning, still got data so going to go processing it...
                }

                // All seams to be enough well so lets convert data to PFS format

                Dictionary<string, FullEOD> ret = new Dictionary<string, FullEOD>();

                foreach (KeyValuePair<string, object> kp in batchItems.Entries)
                {
                    var companyData = JsonSerializer.Deserialize<TwelveTimeSeriesJsonCompany>(kp.Value.ToString());

                    if (companyData.values.Count() != 1)
                        continue;

                    FullEOD eod = new()
                    {
//                        DayTime = DateTime.Now,
  //                      Latest = Decimal.Parse(companyData.values[0].close),                  !!!TODO!!! 2024 - needs at least 'time' there? => Own Intra structure
                        High = Decimal.Parse(companyData.values[0].high),
                        Low = Decimal.Parse(companyData.values[0].low),
                        Open = Decimal.Parse(companyData.values[0].open),
                        PrevClose = -1,     // dont start pulling it here from previous if dont have field, let central place to do pulling from prev day
                        Volume = int.Parse(companyData.values[0].volume),
                    };

                    ret.Add(companyData.meta.symbol, eod);
                }
                return ret;
            }
        }
        catch (Exception e)
        {
            _error = string.Format("TwelveD connection exception {0} for [[{1}]]", e.Message, tickers[0]);
            Log.Warning("TWELVE::GetIntradayAsync() " + _error);
        }
        return null;
    }

    public async Task<Dictionary<string, FullEOD>> GetEodLatestAsync(MarketId marketId, List<string> symbols)
    {
        _error = string.Empty;

        if (string.IsNullOrEmpty(_twelveDataApiKey) == true)
        {
            _error = "TWELVE::Missing private access key!";
            return null;
        }

        int amountOfReqTickers = symbols.Count();

        string twelveJoinedTickers = JoinPfsTickers(marketId, symbols);

        if (string.IsNullOrEmpty(twelveJoinedTickers) == true)
        {
            _error = "TWELVE::Failed, too many tickers!";
            Log.Warning(_error);
            return null;
        }

        await InternalDelayAwait();

        try
        {
            HttpClient tempHttpClient = new HttpClient();


            // RETRY THIS!          MUST RETRY!         Could be way to do both web/server wo having to bring date in... and has lot of interesting data that could expand web functionality w another API      !!!MUST TRY!!!
            // HttpResponseMessage resp = await tempHttpClient.GetAsync($"https://api.twelvedata.com/quote?symbol={twelveJoinedTickers}&eod=true&interval=1day&apikey={_twelveDataApiKey}&format=JSON");



            // 2023-Dec: This seams actually working, tested on open market time.. and as long as would bring wanted date in then returns proper data
            // but atm we dont have date inc yet.. anyway atm looks like this is way to make it on server case...
            //HttpResponseMessage resp = await tempHttpClient.GetAsync($"https://api.twelvedata.com/time_series?symbol={twelveJoinedTickers}&outputsize=1&interval=1day&apikey={_twelveDataApiKey}&previous_close=true&format=JSON&date=2023-12-28");

            // EOD - This is OK for WEB only case, doesnt return open/high/low/volume, but then those are not need on web only... so lets go w this atm...
            HttpResponseMessage resp = await tempHttpClient.GetAsync($"https://api.twelvedata.com/eod?symbol={twelveJoinedTickers}&apikey={_twelveDataApiKey}&format=JSON");

            if (resp.IsSuccessStatusCode == false)
            {
                _error = $"TWELVE::Failed: statusCode={resp.StatusCode} for [[{twelveJoinedTickers}]]";
                Log.Warning(_error);
                return null;
            }

            string content = await resp.Content.ReadAsStringAsync();

            Log.Information($"TWELVE::RESP for {twelveJoinedTickers} content=[{content}]");

            if ( content.StartsWith("{\"code\":404,\"message\":\"**symbol**") )
            {
                _error = $"TWELVE::Failed: For [[{twelveJoinedTickers}]] as {content}";
                Log.Warning(_error);
                return null;
            }

            if (amountOfReqTickers == 1) // Auts, this gives different JSON content depending if requesting 1 ticker or batch 
            {
                var companyData = JsonSerializer.Deserialize<TwelveLatestEodJsonMeta>(content);
                if (companyData == null ) // || companyData.values == null )
                {
                    _error = $"TWELVE::Failed, Empty data: for [[{twelveJoinedTickers}]]";
                    Log.Warning(_error);
                    return null;
                }

                FullEOD eod = new()
                {
                    Date = DateOnly.FromDateTime(DateTime.ParseExact(companyData.datetime, "yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    Close = Decimal.Parse(companyData.close),
                    High = -1,
                    Low = -1,
                    Open = -1,
                    PrevClose = -1,         // !!!TODO!!! TwelveData still needs more work as cant get full EOD for last closing trustable way!
                    Volume = -1,
                };

#if false
                if (eod.Volume == 0)
                {
                    // Follow up! Has seen zero volumes on history side for latest one and while. Keep eye here also
                    _error = string.Format("Failed, Zero volume: {0} for [[{1}]]", resp.StatusCode.ToString(), twelveJoinedTickers);
                    Log.Warning("TWELVE:GetEodLatestAsync() " + _error);
                    return null;
                }
#endif

                Dictionary<string, FullEOD> ret = new();
                ret.Add(companyData.symbol, eod);

                return ret;
            }
            else                                                // BATCH -request
            {
                // Note! This is specially nasty case as top records come w variable names, so we do first Root conversion w dynamic dictionary
                //       and then second level conversion from there a company-by-company to TwelveTimeSeriesJsonCompany format

                var batchItems = JsonSerializer.Deserialize<TwelveTimeSeriesJsonBatchRoot>(content);
                if (batchItems == null || batchItems.Entries == null || batchItems.Entries.Count() == 0)
                {
                    _error = $"TWELVE(BATCH)::Failed, Empty data: for [[{twelveJoinedTickers}]]";
                    Log.Warning(_error);
                    return null;
                }
                else if (batchItems.Entries.Count() < amountOfReqTickers)
                {
                    _error = $"TWELVE(BATCH)::Warning, requested {batchItems.Entries.Count()} got just {amountOfReqTickers} for [[{twelveJoinedTickers}]]";
                    Log.Warning(_error);

                    // This is just warning, still got data so going to go processing it...
                }

                // All seams to be enough well so lets convert data to PFS format

                Dictionary<string, FullEOD> ret = new();

                foreach (KeyValuePair<string, object> kp in batchItems.Entries)
                {
                    var companyData = JsonSerializer.Deserialize<TwelveTimeSeriesJsonCompany>(kp.Value.ToString());

                    if (companyData.values.Count() != 1)
                        continue;

                    FullEOD eod = new()
                    {
                        Date = DateOnly.FromDateTime(DateTime.ParseExact(companyData.values[0].datetime, "yyyy-MM-dd", CultureInfo.InvariantCulture)),
                        Close = Decimal.Parse(companyData.values[0].close),
                        High = Decimal.Parse(companyData.values[0].high),
                        Low = Decimal.Parse(companyData.values[0].low),
                        Open = Decimal.Parse(companyData.values[0].open),
                        PrevClose = -1,     // dont start pulling it here from previous if dont have field, let central place to do pulling from prev day
                        Volume = int.Parse(companyData.values[0].volume),
                    };

                    if (eod.Volume == 0)
                    {
                        // Follow up! Has seen zero volumes on history side for latest one and while. Keep eye here also
                        Log.Warning($"TWELVE:GetEodLatestAsync() {companyData.meta.symbol} skipped as volume is ZERO!");
                    }
                    else
                        ret.Add(companyData.meta.symbol, eod);
                }

                if ( ret.Count == 0 )
                {
                    _error = $"TWELVE::Failed, ALL Zero volume? [[{twelveJoinedTickers}]]";
                    Log.Warning(_error);
                    return null;
                }
                return ret;
            }
        }
        catch ( Exception e )
        {
            _error = $"TWELVE::Exception [{e.Message}] for [[{twelveJoinedTickers}]]";
            Log.Warning("TWELVE::GetEodLatestAsync() " + _error);
        }
        return null;
    }

    public async Task<Dictionary<string, List<FullEOD>>> GetEodHistoryAsync(MarketId marketId, List<string> tickers, DateTime startDay, DateTime endDay)
    {
        _error = string.Empty;

        if (string.IsNullOrEmpty(_twelveDataApiKey) == true)
        {
            _error = "TWELVE::GetEodHistoryAsync() Missing private access key!";
            return null;
        }

        if (tickers.Count != 1)
        {
            _error = "Failed, as using CSV, and PFS doesnt create history jobs w multiple tickers, enforcing a one ticker limit here!";
            Log.Warning("TWELVE:GetEodLatestAsync() " + _error);
            return null;
        }

        string twelveTicker = PfsToTwelveTicker(marketId, tickers[0]);

        await InternalDelayAwait();

        try
        {
            HttpClient tempHttpClient = new HttpClient();

            string start_date = startDay.ToString("yyyy-MM-dd");
            string end_date = endDay.ToString("yyyy-MM-dd");

            HttpResponseMessage resp = await tempHttpClient.GetAsync($"https://api.twelvedata.com/time_series?symbol={twelveTicker}&start_date={start_date}&end_date={end_date}&previous_close=true&interval=1day&apikey={_twelveDataApiKey}&format=CSV");

            if (resp.IsSuccessStatusCode == false)
            {
                _error = string.Format("Failed: {0} for [[{1}]]", resp.StatusCode.ToString(), tickers[0]);
                Log.Warning("TWELVE::GetEodHistoryAsync() " + _error);
                return null;
            }

            CsvConfiguration conf = new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = ";" };

            string content = await resp.Content.ReadAsStringAsync();

            Log.Information($"{twelveTicker} content=[{content}]");

            var csv = new CsvReader(new StringReader(content), conf);

            var stockRecords = csv.GetRecords<TwelveTimeSeriesCsvFormat>().ToList();

            if (stockRecords == null || stockRecords.Count() == 0)
            {
                _error = string.Format("Failed, empty data: {0} for [[{1}]]", resp.StatusCode.ToString(), tickers[0]);
                Log.Warning("TWELVE::GetEodHistoryAsync() " + _error);
                return null;
            }

            Dictionary<string, List<FullEOD>> ret = new Dictionary<string, List<FullEOD>>();
            List<FullEOD> pfsRecords = new();

            foreach (TwelveTimeSeriesCsvFormat twelve in stockRecords)
            {
                if (twelve.volume == 0)
                    // Carefull! Sometimes returns records for 'opening day', so has one day newer than latest market closing day!
                    //           Maybe something to do w pre-market? Who knows but atm can just cut it off w volume == zero
                    continue;

                pfsRecords.Add(new()
                {
                    Date = new DateOnly(twelve.datetime.Year, twelve.datetime.Month, twelve.datetime.Day),
                    Close = twelve.close,
                    High = twelve.high,
                    Low = twelve.low,
                    Open = twelve.open,
                    PrevClose = -1,     // dont start pulling it here from previous if dont have field, let central place to do pulling from prev day
                    Volume = twelve.volume,
                });
            }

            if (pfsRecords.Count() == 0)
            {
                Log.Warning($"Failed with Twelve fetch, got records {stockRecords.Count} but its all with zero volume => rejected");
                return null;
            }

            pfsRecords.Reverse();
            ret.Add(tickers[0], pfsRecords);

            return ret;
        }
        catch (Exception e)
        {
            _error = string.Format("Failed, connection exception {0} for [[{1}]]", e.Message, tickers[0]);
            Log.Warning("TWELVE::GetEodHistoryAsync() " + _error);
        }
        return null;
    }

    public async Task<(DateTime UTC, Dictionary<CurrencyId, decimal> rates)>
        GetCurrencyLatestAsync(CurrencyId toCurrency, CurrencyId fromCurrency = CurrencyId.Unknown)
    {
        _error = string.Empty;

        if (string.IsNullOrEmpty(_twelveDataApiKey) == true)
        {
            _error = "TWELVE::GetCurrencyLatestAsync() Missing private access key!";
            return (UTC: DateTime.MinValue, rates: null);
        }

        await InternalDelayAwait();

        string combo = $"{fromCurrency}/{toCurrency}";

        if ( fromCurrency == CurrencyId.Unknown )
        {
            List<string> batch = new();

            foreach (CurrencyId currency in Enum.GetValues(typeof(CurrencyId)))
            {
                if (currency == CurrencyId.Unknown || currency == toCurrency)
                    continue;

                batch.Add($"{currency}/{toCurrency}");
            }
            combo = String.Join(',', batch);
        }

        try
        {
            HttpClient tempHttpClient = new HttpClient();
            tempHttpClient.Timeout = TimeSpan.FromSeconds(20);

            HttpResponseMessage resp = await tempHttpClient.GetAsync($"https://api.twelvedata.com/exchange_rate?symbol={combo}&interval=1day&apikey={_twelveDataApiKey}");

            if (resp.IsSuccessStatusCode == false)
            {
                _error = string.Format("Failed: {0} for [[{1}]]", resp.StatusCode.ToString(), combo);
                Log.Warning("TWELVE::GetCurrencyLatestAsync() " + _error);
                return (UTC: DateTime.MinValue, rates: null);
            }

            string content = await resp.Content.ReadAsStringAsync();

            if (fromCurrency != CurrencyId.Unknown)   // SINGLE -request
            {
                var twelveCurrency = JsonSerializer.Deserialize<TwelveTimeSeriesJsonSingleRate>(content);

                if (twelveCurrency == null || string.IsNullOrWhiteSpace(twelveCurrency.symbol) || twelveCurrency.rate == 0)
                {
                    _error = string.Format("Failed, empty data: {0} for [[{1}]]", resp.StatusCode.ToString(), combo);
                    Log.Warning("TWELVE::GetCurrencyLatestAsync() " + _error);
                    return (UTC: DateTime.MinValue, rates: null);
                }
                DateTime dateUTC = DateTimeOffset.FromUnixTimeSeconds(twelveCurrency.timestamp).DateTime;
                // All seams to be enough well so lets convert data to PFS format
                Dictionary<CurrencyId, decimal> ret = new();
                ret.Add(fromCurrency, (decimal)twelveCurrency.rate);

                return (UTC: dateUTC, rates: ret);
            }
            else                                        // BATCH -request
            {
                // Note! This is specially nasty case as top records come w variable names, so we do first Root conversion w dynamic dictionary
                //       and then second level conversion from there a company-by-company to TwelveTimeSeriesJsonCompany format

                var batchItems = JsonSerializer.Deserialize<TwelveTimeSeriesJsonBatchRoot>(content);
                if (batchItems == null || batchItems.Entries == null || batchItems.Entries.Count() == 0)
                {
                    _error = string.Format("Failed, Empty data: {0} for [[{1}]]", resp.StatusCode.ToString(), combo);
                    Log.Warning("TWELVE:GetEodLatestAsync() " + _error);
                    return (UTC: DateTime.MinValue, rates: null);
                }

                // All seams to be enough well so lets convert data to PFS format

                DateTime dateUTC = DateTime.MinValue;
                Dictionary<CurrencyId, decimal> ret = new();

                foreach (KeyValuePair<string, object> kp in batchItems.Entries)
                {
                    var twelveCurrency = JsonSerializer.Deserialize<TwelveTimeSeriesJsonSingleRate>(kp.Value.ToString());

                    if (twelveCurrency == null || string.IsNullOrWhiteSpace(twelveCurrency.symbol) || twelveCurrency.rate == 0)
                    {
                        _error = string.Format("Failed, empty data: {0} for [[{1}]]", resp.StatusCode.ToString(), combo);
                        Log.Warning("TWELVE::GetCurrencyLatestAsync() " + _error);
                        return (UTC: DateTime.MinValue, rates: null);
                    }

                    // Actually each has own time, but well worry that later when feels like worrying it... could return oldests ones stamp
                    dateUTC = DateTimeOffset.FromUnixTimeSeconds(twelveCurrency.timestamp).DateTime;

                    string currStr = kp.Key.Substring(0, kp.Key.IndexOf('/'));
                    CurrencyId curr = (CurrencyId)Enum.Parse(typeof(CurrencyId), currStr);
                    ret.Add(curr, (decimal)twelveCurrency.rate);
                }
                return (UTC: dateUTC, rates: ret);
            }
        }
        catch (Exception e)
        {
            _error = string.Format("Connection exception {0} for [[{1}]]", e.Message, combo);
            Log.Warning("TWELVE::GetCurrencyLatestAsync() " + _error);
        }
        return (UTC: DateTime.MinValue, rates: null);
    }

    public async Task<Dictionary<CurrencyId, decimal>>
        GetCurrencyHistoryAsync(DateOnly date, CurrencyId toCurrency, CurrencyId fromCurrency = CurrencyId.Unknown)
    {
        _error = string.Empty;

        if (string.IsNullOrEmpty(_twelveDataApiKey) == true)
        {
            _error = "TWELVE::GetCurrencyHistoryAsync() Missing private access key!";
            return null;
        }

        if (fromCurrency == CurrencyId.Unknown)
            return null; // Actually pfs doesnt implement twelves batch fetch atm for history, can add if used later

        await InternalDelayAwait();

        string combo = string.Format("{0}/{1}", fromCurrency.ToString(), toCurrency.ToString());
        string datestr = date.ToString("yyyy-MM-dd");

        try
        {
            HttpClient tempHttpClient = new HttpClient();
            tempHttpClient.Timeout = TimeSpan.FromSeconds(20);

            HttpResponseMessage resp = await tempHttpClient.GetAsync($"https://api.twelvedata.com/exchange_rate?symbol={combo}&date={datestr}&apikey={_twelveDataApiKey}");

            if (resp.IsSuccessStatusCode == false)
            {
                _error = string.Format("Failed: {0} for [[{1}]]", resp.StatusCode.ToString(), combo);
                Log.Warning("TWELVE::GetCurrencyHistoryAsync() " + _error);
                return null;
            }

            string content = await resp.Content.ReadAsStringAsync();

            var twelveCurrency = JsonSerializer.Deserialize<TwelveTimeSeriesJsonSingleRate>(content);

            if (twelveCurrency == null || string.IsNullOrWhiteSpace(twelveCurrency.symbol) || twelveCurrency.rate == 0)
            {
                _error = string.Format("Failed, empty data: {0} for [[{1}]]", resp.StatusCode.ToString(), combo);
                Log.Warning("TWELVE::GetCurrencyHistoryAsync() " + _error);
                return null;
            }
            DateTime dateUTC = DateTimeOffset.FromUnixTimeSeconds(twelveCurrency.timestamp).DateTime;
            // All seams to be enough well so lets convert data to PFS format
            Dictionary<CurrencyId, decimal> ret = new();
            ret.Add(fromCurrency, (decimal)twelveCurrency.rate);

            return ret;
        }
        catch (Exception e)
        {
            _error = string.Format("Connection exception {0} for [[{1}]]", e.Message, combo);
            Log.Warning("TWELVE::GetCurrencyHistoryAsync() " + _error);
        }
        return null;
    }

    public async Task<StockMeta[]> FindBySymbolAsync(string symbol, MarketId[] markets)
    {
        _error = string.Empty;

        try
        {
            HttpClient tempHttpClient = new HttpClient();

            HttpResponseMessage resp = await tempHttpClient.GetAsync($"https://api.twelvedata.com/stocks?symbol={symbol}");

            if (resp.IsSuccessStatusCode == false)
            {
                _error = $"TWELVE(symbol)::Failed: statusCode={resp.StatusCode} for [[{symbol}]]";
                Log.Warning(_error);
                return null;
            }

            string content = await resp.Content.ReadAsStringAsync();

            Log.Information($"TWELVE(symbol)::RESP for {symbol} content=[{content}]");

            var companyMeta = JsonSerializer.Deserialize<MetaRoot>(content);
            if (companyMeta == null || companyMeta.data == null )
            {
                _error = $"TWELVE(symbol)::Failed, Empty data: for [[{symbol}]]";
                Log.Warning(_error);
                return null;
            }

            List<StockMeta> ret = new();
            foreach (MetaStock ms in companyMeta.data)
            {
                if (Enum.TryParse(ms.exchange, out MarketId marketId) == false)
                    continue;

                ret.Add(new StockMeta(marketId, symbol, ms.name, CurrencyId.Unknown));
            }
            return ret.ToArray();
        }
        catch (Exception e)
        {
            _error = $"TWELVE::Exception [{e.Message}] for [[{symbol}]]";
            Log.Warning("TWELVE::FindBySymbolAsync() " + _error);
        }
        return null;
    }

    static public string JoinPfsTickers(MarketId marketId, List<string> pfsTickers) // symbol=ETH/BTC:Huobi,TRP:TSX,INFY:BSE
    {
        if (pfsTickers.Count > _maxTickers)
            // Coding error, should have divided this task to multiple parts
            return string.Empty;

        return string.Join(',', pfsTickers.ConvertAll<string>(s => PfsToTwelveTicker(marketId, s)));
    }

    static protected string PfsToTwelveTicker(MarketId marketId, string ticker)
    {
        if (marketId == MarketId.AMEX) // Later! Could well keep ticker as it is, and just put market to separate 'exchange' -field
            return $"{ticker}:NYSE";    //        this way joined string is way smaller when market is not repeated! Todo!

        return $"{ticker}:{marketId}";
    }

    // !!!REMEMBER!!! JSON!!! https://json2csharp.com/json-to-csharp

    // content = '{"symbol":"CAD/USD","rate":0.7972,"timestamp":1654603200}'

    public class TwelveTimeSeriesJsonSingleRate
    {
        public string symbol { get; set; }
        public double rate { get; set; }
        public int timestamp { get; set; }
    }


    public class TwelveTimeSeriesJsonMeta
    {
        public string symbol { get; set; }
        public string interval { get; set; }
        public string currency { get; set; }
        public string exchange_timezone { get; set; }
        public string exchange { get; set; }
        public string mic_code { get; set; }
        public string type { get; set; }
    }

    public class TwelveTimeSeriesJsonBatchRoot
    {
        [JsonExtensionData]
        public Dictionary<string, object> Entries { get; set; }
    }

    public class TwelveTimeSeriesJsonCompany
    {
        public TwelveTimeSeriesJsonMeta meta { get; set; }
        public List<TwelveTimeSeriesJsonValue> values { get; set; }
        public string status { get; set; }
    }

    public class TwelveTimeSeriesJsonValue
    {
        public string datetime { get; set; }
        public string open { get; set; }
        public string high { get; set; }
        public string low { get; set; }
        public string close { get; set; }
        public string volume { get; set; }
    }

    public class TwelveLatestEodJsonMeta // for /eod
    {
        public string symbol { get; set; }
        public string exchange { get; set; }
        public string mic_code { get; set; }
        public string currency { get; set; }
        public string datetime { get; set; }
        public long timestamp { get; set; }
        public string close { get; set; }
        public string type { get; set; }
    }

    /*
        datetime;open;high;low;close;volume
        2022-06-06;48.11000;48.11000;47.40000;47.64000;0
        2022-06-03;47.70000;48.20000;47.59000;47.75000;621537
        2022-06-02;46.80000;47.60000;46.45000;47.43000;451163
        2022-06-01;47.80000;47.96000;46.35000;46.49000;688558
    */
    [DataContract]
    private class TwelveTimeSeriesCsvFormat // History we can use CSV format, but its not supporte for batch requests
    {
        [DataMember] public DateTime datetime { get; set; }
        [DataMember] public decimal open { get; set; }
        [DataMember] public decimal high { get; set; }
        [DataMember] public decimal low { get; set; }
        [DataMember] public decimal close { get; set; }
        [DataMember] public int volume { get; set; }
    }

    public class MetaStock
    {
        public string symbol { get; set; }
        public string name { get; set; }
        public string currency { get; set; }
        public string exchange { get; set; }
        public string mic_code { get; set; }
        public string country { get; set; }
        public string type { get; set; }
    }

    public class MetaRoot
    {
        public List<MetaStock> data { get; set; }
        public string status { get; set; }
    }
}
