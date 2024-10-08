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

using System.Text;
using System.Xml.Linq;

using Serilog;

using Pfs.Types;

namespace Pfs.Data;

// Blazor specific implementation of EOD storage, and stock data giving. 
public class StoreLatestEod : IEodLatest, IEodHistory, IDataOwner
{
    protected const string _componentName = "eod";
    protected readonly IPfsPlatform _platform;
    protected readonly IPfsStatus _pfsStatus;
    protected readonly IStockMeta _stockMetaProv;

    /* Most used functionality from here is to get latest closing, but for extra information here
     * is hold also past months closing valuations (no history fetching, just collecting day-by-day)
     */

    protected struct StoreData
    {
        public DateOnly Date { get; set; }                      // newest market day that we got data on one/many stocks
        public Dictionary<string, StockData> Data { get; set; } // sRef as key list of per stock collected EOD etc data

        public StoreData() 
        {
            Date = DateOnly.MinValue;
            Data = new Dictionary<string, StockData>();
        }
    }

    protected StoreData _d;

    public StoreLatestEod(IPfsPlatform pfsPlatform, IPfsStatus pfsStatus, IStockMeta stockMetaProv)
    {
        _platform = pfsPlatform;
        _pfsStatus = pfsStatus;
        _stockMetaProv = stockMetaProv;

        LoadStorageContent();
    }

    protected void Init()
    {
        _d = new StoreData();
    }

    public void Store(MarketId marketId, string symbol, FullEOD[] data)
    {
        FullEOD EOD = data[0];
        string sRef = $"{marketId}${symbol}";

        StockMeta sm = _stockMetaProv.Get(marketId, symbol);

        if (sm == null || data.Length == 0)
            return; // stock is not anymore tracked so just ignore data for it

        if (EOD.Date > _d.Date )
        {
            if (_d.Date != DateOnly.MinValue && EOD.Date.Month != _d.Date.Month)
            {
                List<string> remove = new();
                foreach (KeyValuePair<string, StockData> kvp in _d.Data)
                    // As months have diff amount of market days, on moving next month needs clean unused records from end
                    kvp.Value.ResetEndOfMonthAfter(_d.Date);

                foreach ( string r in remove)
                    _d.Data.Remove(r);
            }

            if (_d.Date != DateOnly.MinValue)
            {
                foreach (KeyValuePair<string, StockData> kvp in _d.Data)
                {
                    // So when ever we get new data, we clean from previous day + 1 to that day from all stocks!
                    for (DateOnly clear = _d.Date.AddWorkingDays(+1); clear <= EOD.Date; clear = clear.AddWorkingDays(+1))
                        kvp.Value.ResetDate(clear);
                }
            }
            _d.Date = EOD.Date;
        }

        if (_d.Data.ContainsKey(sRef) == false)
        {
            _d.Data.Add(sRef, new StockData());
        }
        else
        {
            FullEOD prevEOD = GetFullEOD(marketId, symbol);

            if (prevEOD != null && EOD.PrevClose < 0)
                // Note! Not always correct, could check also that prev market day
                EOD.PrevClose = prevEOD.Close;
        }

        _d.Data[sRef].SetEOD(EOD);

        EventNewUnsavedContent?.Invoke(this, _componentName);

        _ = _pfsStatus.SendPfsClientEvent(PfsClientEventId.StoredLatestEod, sRef); // OK? Diff Thread?

        return;
    }

    // !!!LATER!!! for StoreLatestIntraDay() -  Only accept intraday if has already eod, and remove intraday when new eod received

    public FullEOD GetFullEOD(string sRef)                                                          // ILatestEod
    {
        if (_d.Data.ContainsKey(sRef) == false)
            return null;

        return _d.Data[sRef].EOD;
    }

    public FullEOD GetFullEOD(MarketId marketId, string symbol)                                     // ILatestEod
    {
        return GetFullEOD($"{marketId}${symbol}");
    }

    public (decimal close, decimal changeP, decimal min, decimal max) GetWeekChange(string sRef)    // IEodHistory
    {
        if (_d.Data.ContainsKey(sRef) == false)
            return (-1, 0, 0, 0);

        return _d.Data[sRef].CloseWeekAgo();
    }

    public (decimal close, decimal changeP, decimal min, decimal max) GetMonthChange(string sRef)   // IEodHistory
    {
        if (_d.Data.ContainsKey(sRef) == false)
            return (-1, 0, 0, 0);

        return _d.Data[sRef].CloseMonthAgo();
    }

    public (DateOnly, decimal[]) GetLastClosings(string sRef, int amount)                           // IEodHistory
    {
        if (IsValid(sRef) == false)
            return (DateOnly.MinValue, null);

        return (_d.Data[sRef].EOD.Date, _d.Data[sRef].GetLastHistory(amount));
    }

    protected bool IsValid(string sRef)
    {   // Needs to have EOD, and it needs to be from latest date that has received EODs
        if (_d.Data.ContainsKey(sRef) == false)
            return false;

        FullEOD eod = _d.Data[sRef].EOD;

        if (eod == null || eod.Close < 0 || eod.Date < _d.Date)
            return false;

        return true;
    }

    public event EventHandler<string> EventNewUnsavedContent;                                       // IDataOwner
    public string GetComponentName() { return _componentName; }
    public void OnDataInit() { Init(); }
    public void OnDataSaveStorage() { BackupToStorage(); }

    public string CreateBackup()
    {
        return ExportXml();
    }

    public string CreatePartialBackup(List<string> symbols)
    {
        return ExportXml(symbols);
    }

    public Result RestoreBackup(string content)
    {
        try
        {
            _d = ImportXml(content);

            return new OkResult();
        }
        catch ( Exception ex) 
        {   // Note! This is NOT critical error for Restore - as can just refetch!
            Log.Warning($"{_componentName} RestoreBackup failed to exception: [{ex.Message}]");
            Init();
            return new OkResult();
        }
    }

    protected void LoadStorageContent()
    {
        try
        {
            string stored = _platform.PermRead(_componentName);

            if (string.IsNullOrWhiteSpace(stored))
            {
                Init();
                return;
            }

            _d = ImportXml(stored);
        }
        catch (Exception ex)
        {
            Log.Warning($"{_componentName} LoadStorageContent failed to exception: [{ex.Message}]");
            Init();
            _platform.PermRemove(_componentName);
        }
    }

    protected void BackupToStorage()
    {
        _platform.PermWrite(_componentName, CreateBackup());
    }

    protected string ExportXml(List<string> symbols = null)
    {
        XElement rootPFS = new XElement("PFS");

        // Keep this separate as so much more critical information
        XElement eodElem = new XElement("Eod");
        eodElem.SetAttributeValue("Date", _d.Date.ToYMD());
        rootPFS.Add(eodElem);

        foreach (KeyValuePair<string, StockData> kvp in _d.Data)
        {
            (MarketId marketId, string symbol) = StockMeta.ParseSRef(kvp.Key);

            if (symbols != null && symbols.Contains(symbol) == false)
                continue;

            XElement stockElem = new XElement("Data");
            stockElem.SetAttributeValue("SRef", kvp.Key);
            stockElem.SetAttributeValue("EOD", kvp.Value.EOD.GetStoreFormat());
            stockElem.SetAttributeValue("Hist", kvp.Value.GetHistoryStorageFormat());
            eodElem.Add(stockElem);
        }
        return rootPFS.ToString();
    }

    protected StoreData ImportXml(string xml)
    {
        StoreData ret = new();

        XDocument xmlDoc = XDocument.Parse(xml);
        XElement rootPFS = xmlDoc.Element("PFS");

        XElement topElem = rootPFS.Element("Eod");
        if (topElem != null && topElem.HasElements)
        {
            ret.Date = DateOnlyExtensions.ParseYMD((string)topElem.Attribute("Date"));

            foreach (XElement stockElem in topElem.Elements("Data"))
            {
                try
                {
                    StockData sd = new ((string)stockElem.Attribute("EOD"), (string)stockElem.Attribute("Hist"));

                    if (sd.IsEmpty() && sd.EOD.Date < _platform.GetCurrentUtcDate().AddMonths(-1))
                        // there is no history, and last history fetched is over month old -> CLEANUP!
                        continue;

                    ret.Data.Add((string)stockElem.Attribute("SRef"), sd);
                }
                catch ( Exception ex )
                {
                    Log.Warning($"{_componentName} ImportXml failed for {(string)stockElem.Attribute("SRef")} to exception: [{ex.Message}]");
                }
            }
        }
        return ret;
    }

    // Helper class to handle per stock one month history & latest EOD storing
    protected class StockData
    {
        public const int MaxWorkDays = 23;
        protected decimal[] History { get; set; }
        public FullEOD EOD { get; set; }
        protected LatestIntraDay IntraDay = null; // never stored

        public StockData()
        {
            History = new decimal[MaxWorkDays];
            EOD = null;
            IntraDay = null;

            for (int i = 0; i < 23; i++)
                History[i] = -1;
        }

        public StockData(string eodStorageFormat, string historyStorageFormat) : this()
        {
            EOD = new FullEOD(eodStorageFormat);
            UnpackHistoryStorageFormat(historyStorageFormat);
        }

        public bool IsEmpty()
        {
            for (int i = 0; i < 23; i++)
                if (History[i] != -1)
                    return false;
            return true;
        }

        public void ResetDate(DateOnly date)
        {
            History[date.GetWorkingDayOfMonth()] = -1;
            IntraDay = null;
        }

        public void ResetEndOfMonthAfter(DateOnly afterDay)
        {
            // As months have diff amount of market days, on moving next month needs clean unused records from end
            for (int c = afterDay.GetWorkingDayOfMonth() + 1; c < MaxWorkDays; c++)
                History[c] = -1;
        }

        public void SetEOD(FullEOD fullEOD)
        {
            EOD = fullEOD;
            History[fullEOD.Date.GetWorkingDayOfMonth()] = fullEOD.Close;
            IntraDay = null;
        }

        public string GetHistoryStorageFormat()
        {
            StringBuilder sb = new();

            for (int p = 0; p < MaxWorkDays; p++)
            {
                if (History[p] < 0)
                    sb.Append(',');
                else
                    sb.Append(History[p].ToString("0.####") + ',');
            }
            return sb.ToString();
        }
        
        protected void UnpackHistoryStorageFormat(string content)
        {
            string[] hist = content.Split(',');

            for (int p = 0; p < MaxWorkDays; p++)
            {
                if (string.IsNullOrWhiteSpace(hist[p]) == false)
                    History[p] = DecimalExtensions.Parse(hist[p]);
            }
        }

        // GetWeek / Month

        public (decimal close, decimal changeP, decimal min, decimal max) CloseWeekAgo()
        {
            int workDay = EOD.Date.GetWorkingDayOfMonth();
            int histLoc = GetHistDayLocation(EOD.Date, 5);

            if (histLoc < 0)
                return (-1, -1, 0, 0);

            decimal close = History[histLoc];

            decimal change = EOD.Close - close;

            (decimal min, decimal max) = GetMinMax(histLoc, workDay);

            return (close, change == 0 ? change : change / close * 100, min, max);
        }

        public (decimal close, decimal changeP, decimal min, decimal max) CloseMonthAgo()
        {
            int workDay = EOD.Date.GetWorkingDayOfMonth();
            int histLoc = GetMonthlyHistLocation(EOD.Date);

            if (histLoc < 0)
                return (-1, -1, 0, 0);

            decimal close = History[histLoc];

            decimal change = EOD.Close - close;

            (decimal min, decimal max) = GetMinMax(histLoc, workDay);

            return (close, change == 0 ? change : change / close * 100, min, max);
        }

        public decimal[] GetLastHistory(int amount)
        {
            List<decimal> ret = new();
            int from = EOD.Date.GetWorkingDayOfMonth();

            for (; ret.Count() < amount && ret.Count() < 20; from--)
            {
                if (from < 0)
                    from = EOD.Date.AddMonths(-1).GetWorkingDaysOnMonth() - 1;

                ret.Add(History[from]);
            }
            return ret.ToArray();
        }

        protected (decimal min, decimal max) GetMinMax(int from, int to)
        {
            decimal min = -1;
            decimal max = -1;

            for (; from != to; from++ )
            {
                if (from == MaxWorkDays)
                    // really should look months max but as min/max ignores ones without value here it doesnt matter
                    from = 0;

                if (History[from] > 0)
                {
                    if (min == -1 || History[from] < min)
                        min = History[from];

                    if (History[from] > max)
                        max = History[from];
                }

                if (from == to)
                    // on start of month needs this! As forever loop w to=0
                    break;
            }
            return (min, max);
        }

        protected int GetMonthlyHistLocation(DateOnly date)
        {
            // As collecting data just for month, and months are different amount of workdays
            // there is no point trying to solve 'month' problem by minus but just simply 
            // taking value from one of of "next slots" those still has oldest available data.
            // on reality its either next slot, or if thats empty then try next after it
            int todaysWD = date.GetWorkingDayOfMonth();
            int lastMonthWDs = date.AddMonths(-1).GetWorkingDaysOnMonth();

            if (todaysWD + 1 < lastMonthWDs)
            {   // return next after 'today' as thats oldest
                if (History[todaysWD + 1] > 0)
                    return todaysWD + 1;

                // try next one in case had lazy day w data loading
                if (todaysWD + 2 < lastMonthWDs)
                {   
                    if (History[todaysWD + 2] > 0)
                        return todaysWD + 2;
                }
                else if (History[0] > 0) // case second last day of month 
                    return 0;
            }   
            else
            {   // case last day of month -> we need check first ones
                if (History[0] > 0)
                    return 0;

                if (History[1] > 0)
                    return 1;
            }

            // Error! Looking 3th of Sep 2024 month failed, as first of month (monday) was labor day, 
            // and previous month was 21 days.. and one before that 23 days. Above used old 30-Aug
            // data to try figure out this ending -1 month to Jun (too far) and hitting zeros on August
            // fast looking to solve this would require to use 'current date' instead one from latest eod

            return -1;
        }

        protected int GetHistDayLocation(DateOnly date, int minus) // Wrn! Dont use minus > 15 (3wks)
        {
            // Test_WorkDayMinus();

            int workDay = Local_WorkDayMinus(date, minus);
            if (History[workDay] > 0)
                return workDay;

            // Exact asked day was missing data.. lets try closer (day shorter minus)
            workDay = Local_WorkDayMinus(date, minus - 1);
            if (History[workDay] > 0)
                return workDay;

            // No luck, how about day further
            workDay = Local_WorkDayMinus(date, minus + 1);
            if (History[workDay] > 0)
                return workDay;

            return -1;

            int Local_WorkDayMinus(DateOnly date, int minus)
            {
                int workDay = date.GetWorkingDayOfMonth() - minus;

                if (workDay < 0)
                    workDay = date.AddMonths(-1).GetWorkingDaysOnMonth() + workDay; // workDay is negative

                return workDay;
            }
#if false
            void Test_WorkDayMinus()
            {
                List<(string date, int wd, List<(int minus,int wd)> test)> wdm = new List<(string, int, List<(int, int)>)>()
                {
                    ("2024-07-01", 0,   new List<(int, int)> { (5, 15) } ),
                    ("2024-07-18", 13,  new List<(int, int)> { (5, 8) } ),
                };

                foreach (var entry in wdm)
                {
                    int res = DateOnly.Parse(entry.date).GetWorkingDayOfMonth();

                    if (res != entry.wd)
                    {
                        string temp = $"{entry.date} == {entry.wd} not {res}";
                        System.Diagnostics.Debugger.Break();
                    }
                }

                foreach (var entry in wdm)
                {
                    foreach ( var sub in entry.test)
                    {
                        int res = Local_WorkDayMinus(DateOnly.Parse(entry.date), sub.minus);

                        if ( res != sub.wd)
                        {
                            string temp = $"{entry.date} - {sub.minus} = {sub.wd} not {res}";
                            System.Diagnostics.Debugger.Break();
                            res = Local_WorkDayMinus(DateOnly.Parse(entry.date), sub.minus);
                        }
                    }
                }
            }
#endif
        }
    }
}
