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

using Pfs.Types;
using Serilog;
using System.Text;

namespace Pfs.ExtFetch;

internal class FetchEodTask
{
    /* Wrapper around each ExtProvider implementations, taking care of:
     * - Providing busy/available status for its clients
     * - Running actual fetch on separate thread
     * - Keeping count of used credits and setting daily/monthly limits
     * - Having success / fail counters per market for uptime and fetch time
     * - Gets new configs when changed, and takes use when possible
     */

    public enum State
    {
        Disabled = 0,   // PrivKey is not set so cant launch, but may have old one still fetching
        Free,           // Available for fetching, call LaunchFetchEodLatest

        // These are busy modes with normal fetchings
        Fetching,       // busy atm doing fetching
        Ready,          // Fetching finished successfully, call GetOkResult
        Error,          // Fetching failed, call GetErrorResult

        // Test Fetch by user manual request
        Testing,        // Run paraller but waited so just one state
    };

    public static string PrintDayCredits(ProvPermInfo permInfo, int dailyLimit) =>
        $"D{permInfo.DaysCreditLeft}/{dailyLimit} [{permInfo.CreditDateUtc.ToString("yyyy-MMM-dd")}]";

    public static string PrintMonthCredits(ProvPermInfo permInfo, int monthlyLimit) =>
        $"D{permInfo.DaysCreditLeft} M{permInfo.MonthsCreditLeft}/{monthlyLimit} [{permInfo.CreditDateUtc.ToString("yyyy-MMM-dd")}]";


    protected readonly IPfsStatus _pfsStatus;

    private const int MaxErrors = 3;            // After N errors a fetching from that market gets closed

    private IExtProvider _provider;             // access to provider this instance is wrapping
    private IExtDataProvider _data;
    private State _state = State.Disabled;      // Dont read this directly, always use GetState()
    private string _key = null;                 // if no key then disabled
    private MarketId _marketId;                 // target market for fetch operation
    private List<string> _symbols;              // operation symbols
    private DateOnly _expectedDate;

    // Tracking total uptime success/fail rates per provider, but also fetch specific rates
    private int[] _successFetch = new int[Enum.GetNames(typeof(MarketId)).Length];
    private int[] _failedFetch = new int[Enum.GetNames(typeof(MarketId)).Length];
    private int[] _successUptime = new int[Enum.GetNames(typeof(MarketId)).Length];
    private int[] _failedUptime = new int[Enum.GetNames(typeof(MarketId)).Length];

    private Task<Result<Dictionary<string, FullEOD>>> _task;

    public ExtProviderId ProvId { get; internal set; }

    public ProvPermInfo PermInfo { get; internal set; } = null; // stays null if no credits on provider

    public class ProvPermInfo
    {
        public DateOnly CreditDateUtc { get; set; }     // enforcing daily limits over UTC dateonly

        public int? DaysCreditLeft { get; set; }        // If null then no limits

        public int? MonthsCreditLeft { get; set; }
    }

    public void ResetFetchCounters()
    {
        _successFetch = new int[Enum.GetNames(typeof(MarketId)).Length];
        _failedFetch = new int[Enum.GetNames(typeof(MarketId)).Length];
    }   

    public bool IsActive(MarketId marketId) 
    { 
        return string.IsNullOrWhiteSpace(_key) == false && _data.IsMarketSupport(marketId); 
    }

    public MarketId[] GetMarkets()
    {
        if (PermInfo != null && PermInfo.DaysCreditLeft.HasValue && PermInfo.DaysCreditLeft.Value <= 0)
            return Array.Empty<MarketId>(); // this is how we currently stop fetching if out of credits!    THINK! Maybe should have with 'GetState' having 'OutOfCredits' ??

        List<MarketId> markets = new();

        foreach ( MarketId marketId in Enum.GetValues(typeof(MarketId)))
        {
            if (marketId.IsReal() == false || _data.IsMarketSupport(marketId) == false )
                continue;

            if (_failedFetch[(int)marketId] > MaxErrors)
                continue;

            markets.Add(marketId);
        }
        return markets.ToArray();
    }

    public FetchEodTask(IPfsStatus pfsStatus, ExtProviderId provId, IExtProvider provider, IExtDataProvider data, ProvPermInfo permInfo)
    {
        _pfsStatus = pfsStatus;
        _provider = provider;
        _data = data;
        ProvId = provId;
        PermInfo = permInfo;        

        if ( PermInfo == null )
            // Could be that just not setup yet, so check what settings tells from credits
            ResetCreditCounters();
    }

    public int? DailyCreditsLeft() // Only used for info atm
    {
        if (PermInfo != null && PermInfo.DaysCreditLeft != null)
            return PermInfo.DaysCreditLeft.Value;

        return null;
    }

    public void CheckCreditCounters() // call when ever to see if needs reset
    {
        if (PermInfo == null)
            return;

        var limits = GetProvCreditLimits();

        if (limits.monthly > 0 && PermInfo.CreditDateUtc.Month != DateTime.UtcNow.Month )
        {
            Log.Information($"{_provider} CheckCreditCounters new Month causes reset to credit's");
            ResetCreditCounters();
        }
        else if (limits.monthly == 0 && limits.daily > 0 && PermInfo.CreditDateUtc.Day != DateTime.UtcNow.Day)
        {
            Log.Information($"{_provider} CheckCreditCounters new Day causes reset to credit's");
            ResetCreditCounters();
        }
        else if (limits.monthly > 0 && PermInfo.CreditDateUtc.Day != DateTime.UtcNow.Day && PermInfo.MonthsCreditLeft > 0)
        {
            // For monthly credit case, still need to reallocate remaining monthly credits share for new day
            PermInfo.CreditDateUtc = DateOnly.FromDateTime(DateTime.UtcNow);
            PermInfo.DaysCreditLeft = PermInfo.MonthsCreditLeft / Math.Max(MonthsRemainingWorkDays(), 1);
            Log.Information($"{_provider} CheckCreditCounters is month credited, and new date has {PermInfo.DaysCreditLeft} credits for today");
        }
    }

    public void ResetCreditCounters()
    {
        var limits = GetProvCreditLimits();

        if (limits.daily > 0)
        {
            PermInfo = new ProvPermInfo()
            {   // This specific provider has daily credit quota
                CreditDateUtc = DateOnly.FromDateTime(DateTime.UtcNow),
                DaysCreditLeft = limits.daily,
                MonthsCreditLeft = null,
            };
            Log.Information($"{_provider} ResetCreditCounters DAILY to {PrintDayCredits(PermInfo, limits.daily)}");
            return;
        }

        if (limits.monthly > 0)
        {
            PermInfo = new ProvPermInfo()
            {
                CreditDateUtc = DateOnly.FromDateTime(DateTime.UtcNow),
                DaysCreditLeft = limits.monthly / Math.Max(MonthsRemainingWorkDays(), 1),
                MonthsCreditLeft = limits.monthly,
            };
            Log.Information($"{_provider} ResetCreditCounters MONTHLY to {PrintMonthCredits(PermInfo, limits.monthly)}");
            return;
        }

        PermInfo = null;
        return;
    }

    protected void DeductDayMonthCredits()
    {
        int price = _provider.GetLastCreditPrice();

        if (PermInfo != null && price > 0)
        {
//            CheckCreditCounters(); Why this is need?

            if (PermInfo.DaysCreditLeft.HasValue)
                PermInfo.DaysCreditLeft = PermInfo.DaysCreditLeft.Value - price;

            if (PermInfo.MonthsCreditLeft.HasValue)
                PermInfo.MonthsCreditLeft = PermInfo.MonthsCreditLeft.Value - price;
        }
    }

    protected (int daily, int monthly) GetProvCreditLimits()
    {
        int daily = _pfsStatus.GetAppCfg($"{ProvId}DayCredits");
        int monthly = _pfsStatus.GetAppCfg($"{ProvId}MonthCredits");

        return (daily, monthly);
    }

    protected int MonthsRemainingWorkDays()
    {
        int ret = 0;
        DateTime day = DateTime.UtcNow;
        int month = day.Month;
        if (day.DayOfWeek != DayOfWeek.Saturday&& day.DayOfWeek != DayOfWeek.Sunday)
            ret++;
        for (; month == day.Month; day = day.AddWorkingDays(+1))
            ret++;

        return ret;
    }

    public void SetPrivKey(string privKey)
    {
        if ( string.IsNullOrWhiteSpace(privKey) == false)
        {
            if (string.IsNullOrWhiteSpace(_key) == false && privKey != _key)
                // If has key, and gets new key then reset credit counters
                ResetCreditCounters();

            _key = new string(privKey);
            _provider.SetPrivateKey(_key);
        }
        else
        {
            _key = null;
            _provider.SetPrivateKey(string.Empty);
        }
    }

    public void LaunchFetchEodLatest(MarketId marketId, List<string> symbols, DateOnly expectedDate)
    {
        if (GetState() != State.Free)
            throw new Exception("CodingError! LaunchFetchEodLatest called on wrong state!");

        _marketId = marketId;
        _symbols = symbols;
        _state = State.Fetching;
        _expectedDate = expectedDate;

        _task = Task.Run(() => RunFetchAsWorkerThread(marketId, symbols));
        _task.ConfigureAwait(false);
    }

    public State GetState() // Always use this as only true state
    {
        if (_state == State.Ready || _state == State.Fetching || _state == State.Testing || _state == State.Error)
            return _state;  // Even if disabled on fly, still ongoing fetching is finished

        if (string.IsNullOrWhiteSpace(_key) == false)               // !!!THINK!!! 'OutOfCredits' would be more logical here!
            return State.Free;

        return State.Disabled;
    }

    public Dictionary<MarketId, (int fail, int success)> GetFetchInfo() // fetch info mainly shown UIs fetch dialog
    {
        Dictionary<MarketId, (int fail, int success)> ret = new();

        foreach ( MarketId marketId in Enum.GetValues(typeof(MarketId)))
        {
            if (_successFetch[(int)marketId] == 0 && _failedFetch[(int)marketId] == 0)
                continue;

            ret.Add(marketId, (_failedFetch[(int)marketId], _successFetch[(int)marketId]));
        }
        return ret;
    }

    public string GetStatusInfo() // STATUS, Credit: D12/100 [yyyy-mmm-dd]or D12 M900/1000, NYSE F1 OK100 NASDAQ...
    {
        StringBuilder sb = new();

        sb.Append($"{GetState()} ");

        if ( PermInfo != null )
        {
            var limits = GetProvCreditLimits();

            if (PermInfo.MonthsCreditLeft.HasValue == false)
                sb.Append(",Credits: " + PrintDayCredits(PermInfo, limits.daily));

            else
                sb.Append(",Credits: " + PrintMonthCredits(PermInfo, limits.monthly));
        }

        foreach ( MarketId marketId in Enum.GetValues(typeof(MarketId)))
        {
            if (marketId.IsReal() == false)
                continue;

            if (_successUptime[(int)marketId] == 0 && _failedUptime[(int)marketId] == 0)
                continue;

            sb.Append($",{marketId} F{_failedUptime[(int)marketId]} OK{_successUptime[(int)marketId]} ");
        }
        return sb.ToString();
    }

    public (MarketId marketId, Dictionary<string, FullEOD> eod) GetOkResult()
    {
        if (GetState() != State.Ready)
            return (MarketId.Unknown, null);

        Result<Dictionary<string, FullEOD>> eodResult = _task.Result;

        DeductDayMonthCredits();
        _state = State.Free;

        return (_marketId, eodResult.Data);
    }

    public (MarketId marketId, List<string> symbols, string errMsg) GetErrorResult()
    {
        if (GetState() != State.Error)
            return (MarketId.Unknown, null, "wrong state");

        DeductDayMonthCredits();
        _state = State.Free;

        return (_marketId, _symbols, (_task.Result as FailResult<Dictionary<string, FullEOD>>).Message);
    }

    public async Task<Result<FullEOD>> TestStockFetchingAsync(MarketId marketId, string symbol)
    {
        if (GetState() != State.Free)
            return new FailResult<FullEOD>("Cant do! Not available atm!");

        try
        {
            _state = State.Testing;

            Dictionary<string, FullEOD> provResp = await _data.GetEodLatestAsync(marketId, new List<string>([symbol]));

            _state = State.Free;

            if ( provResp == null )
                return new FailResult<FullEOD>($"Fetch failed! {_provider.GetLastError()}");

            return new OkResult<FullEOD>(provResp[symbol]);

        }
        catch( Exception ex)
        {
            _state = State.Free;
            return new FailResult<FullEOD>($"Failed exception! {ex.Message}");
        }
    }

    protected async Task<Result<Dictionary<string, FullEOD>>> RunFetchAsWorkerThread(MarketId marketId, List<string> symbols)
    {
        // !!!LATER!!!  Need to convert symbols per ~ if has alternative symbol given by user on fetch rule

        Dictionary<string, FullEOD> provResp = await _data.GetEodLatestAsync(marketId, symbols);

        if (provResp == null || provResp.Count == 0)
        {
            _failedFetch[(int)_marketId]++;
            _failedUptime[(int)_marketId]++;
            _state = State.Error;
            return new FailResult<Dictionary<string, FullEOD>>(_provider.GetLastError());
        }

        if (provResp.First().Value.Date < _expectedDate)
        {   // Decision! This '_expectedDate' is coming from markets last closing. So anything older gets 
            // instantly rejected. Teoretically could have situation where has week old data and getting 
            // few days older would be improvement. => Dont care, its latest or nothing! 
            // Reason is that after all this is exception case, and fixing it would mean that this fetch
            // component would need to know whats latest data.. and dont wanna do useless dependencies!
            _failedFetch[(int)_marketId]++;
            _failedUptime[(int)_marketId]++;
            _state = State.Error;
            return new FailResult<Dictionary<string, FullEOD>>($"ERROR: From {_provider} to {marketId} was expecting {_expectedDate} but got older {provResp.First().Value.Date}");
        }

        // !!!LATER!!!  Need to convert resp back to real symbol if used ~ to alternate ticker for this provider

        _successFetch[(int)_marketId]++;
        _successUptime[(int)_marketId]++;
        _state = State.Ready;
        return new OkResult<Dictionary<string, FullEOD>>(provResp);
    }
}
