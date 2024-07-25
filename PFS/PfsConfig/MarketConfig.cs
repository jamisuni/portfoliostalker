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
using System.Collections.Immutable;

using Pfs.Helpers;
using Pfs.Types;

namespace Pfs.Config;

public class MarketConfig : IMarketMeta, IPfsSetMarketConfig, ICmdHandler, IDataOwner
{
    protected const string _componentName = "cfgmarket";

    protected IPfsPlatform _platform;

    protected readonly static ImmutableArray<string> _cmdTemplates = [
        "list"
    ];

    /* PLAN:
     * - Minimal dependencies to anywhere, does not know from stocks nor if has provider settings
     * - 'IMarketMeta' is access interface to get meta & information from actives & last/next closings
     * - Allows to enable/disable markets per user preferences
     * - Allows to set holidays when market is fully closed
     * - 'CLOSED' is special market thats final rest place for retired tickers
     * 
     * Decision! MarketId stays as enum, so adding totally new markets requires new compilation
     * 
     * Decision! Minimum time to fetch is this side, defined as a one value for full market. We need to 
     *           have minimum time so can automize fethching on EodSrv side. Having it this time gives
     *           one clear value and leaves for user decision how that fits to used providers.
     */

    protected Dictionary<MarketId, MarketUserInfo> _configs;

    protected class MarketUserInfo
    {
        public bool Active { get; set; } = false;

        public DateOnly[] Holidays = null; // to speed up things, runtime holidays are unpacked here

        public string HolidaysStorageFormat { get; set; } = "";

        public int MinFetchMins { get; set; } = 0;          // how long after market close stocks can be fetched
    }

    public MarketConfig(IPfsPlatform platform)
    {
        _platform = platform;

        LoadStorageContent();
    }

    protected void Init()
    {
        _configs = new();
    }

    public IEnumerable<MarketMeta> GetActives()
    {
        foreach (MarketDef md in _marketDef )
            if ( _configs.ContainsKey(md.Market.ID) && _configs[md.Market.ID].Active )
                yield return md.Market;
    }

    public MarketMeta Get(MarketId marketId)
    {
        return _marketDef.Single(d => d.Market.ID == marketId).Market;
    }

    public MarketStatus[] GetMarketStatus()
    {
        List<MarketStatus> ret = new();

        foreach (MarketDef md in _marketDef )
        {
            MarketUserInfo info = null;

            if ( _configs.ContainsKey(md.Market.ID) )
                info = _configs[md.Market.ID];

            bool active = info?.Active ?? false;

            var last = LastClosing(md.Market.ID);

            ret.Add(new MarketStatus(md.Market, active, last.localDate, last.utcTime, NextClosingUtc(md.Market.ID), info?.MinFetchMins ?? 1));
        }
        return ret.ToArray();
    }

    public (DateOnly localDate, DateTime utcTime) LastClosing(MarketId marketId)
    {
        MarketDef marketSett = _marketDef.Single(d => d.Market.ID == marketId);

        TimeZoneInfo marketTimezone = TimeZoneInfo.FindSystemTimeZoneById(marketSett.TimezoneTag);
        DateTime marketLocalTime = TimeZoneInfo.ConvertTimeFromUtc(_platform.GetCurrentUtcTime(), marketTimezone);

        // Next figure out what should be closing day+time of market, per XML hardcoded market HHMM closing time
        DateTime marketLocalClosing = new DateTime(marketLocalTime.Year, marketLocalTime.Month, marketLocalTime.Day,
                                                   marketSett.localClosingHour, marketSett.localClosingMin, 0);

        // Make sure falls back to Friday if atm living weekend
        if (marketLocalClosing.DayOfWeek == DayOfWeek.Saturday)
            marketLocalClosing = marketLocalClosing.AddDays(-1);

        else if (marketLocalClosing.DayOfWeek == DayOfWeek.Sunday)
            marketLocalClosing = marketLocalClosing.AddDays(-2);

        if (marketLocalClosing < marketLocalTime)
        {   // This case market has 'just' closed... 
        }
        else
        {   // This case we are atm open or waiting to open.. so fall back previous closing
            marketLocalClosing = marketLocalClosing.AddWorkingDays(-1);
        }

        while (_configs.ContainsKey(marketId) && Array.Exists(_configs[marketId].Holidays, h => h == DateOnly.FromDateTime(marketLocalClosing)) )
        {   // Finally check against holidays information and move further if happen to be on user defined market holiday
            marketLocalClosing = marketLocalClosing.AddWorkingDays(-1);
        }

        return (DateOnly.FromDateTime(marketLocalClosing),
                TimeZoneInfo.ConvertTimeToUtc(marketLocalClosing, marketTimezone));
    }

    public DateTime NextClosingUtc(MarketId marketId)
    {
        var next = LastClosing(marketId).utcTime.AddWorkingDays(+1);

        while (_configs.ContainsKey(marketId) && Array.Exists(_configs[marketId].Holidays, h => h == DateOnly.FromDateTime(next)))
        {   // Finally check against holidays information and move further if happen to be on user defined market holiday
            next = next.AddWorkingDays(+1);
        }
        return next;
    }

    public MarketCfg GetCfg(MarketId marketId)
    {
        if (_configs.ContainsKey(marketId) == false)
            return null;

        MarketUserInfo info = _configs[marketId];

        return new MarketCfg(info.Active, info.HolidaysStorageFormat, info.MinFetchMins);
    }

    public bool SetCfg(MarketId marketId, MarketCfg cfg)
    {
        DateOnly[] holidays = MarketHolidays.GetDates(cfg.Holidays);

        if (holidays == null /*invalid format given*/)
            return false; 

        if (_configs.ContainsKey(marketId) == false)
        {
            _configs.Add(marketId, new MarketUserInfo()
            {
                Active = cfg.Active,
                HolidaysStorageFormat = cfg.Holidays,
                Holidays = holidays,
                MinFetchMins = cfg.minFetchMins,
            });
        }
        else
        {
            _configs[marketId].Active = cfg.Active;
            _configs[marketId].HolidaysStorageFormat = cfg.Holidays;
            _configs[marketId].Holidays = holidays;
            _configs[marketId].MinFetchMins = cfg.minFetchMins;
        }

        EventNewUnsavedContent?.Invoke(this, _componentName);
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
        return ExportXml();
    }

    public Result RestoreBackup(string content)
    {
        try
        {
            _configs = ImportXml(content);
            RecreateHolidayDateOnlys();
            return new OkResult();
        }
        catch (Exception e)
        {
            return new FailResult($"MarketConfig: Exception: {e.Message}");
        }
    }

    protected void LoadStorageContent()
    {
        try
        {
            Init();

            string xml = _platform.PermRead(_componentName);

            if ( string.IsNullOrWhiteSpace(xml) )
            {   // To simplify taking use, if nothing is stored we default to NYSE/NASDAQ as active
                _configs.Add(MarketId.NASDAQ,   new MarketUserInfo() { Active = true, });
                _configs.Add(MarketId.NYSE,     new MarketUserInfo() { Active = true, });

                RecreateHolidayDateOnlys();
                return;
            }

            _configs = ImportXml(xml);
            RecreateHolidayDateOnlys();
        }
        catch
        {
            Init();
            _platform.PermRemove(_componentName);
        }
    }

    protected void BackupToStorage()
    {
        _platform.PermWrite(_componentName, ExportXml());
    }

    protected void RecreateHolidayDateOnlys()
    {
        foreach ( KeyValuePair< MarketId, MarketUserInfo> kvp in _configs)
            kvp.Value.Holidays = MarketHolidays.GetDates(kvp.Value.HolidaysStorageFormat);
    }

    protected string ExportXml()
    {
        XElement rootPFS = new XElement("PFS");

        // Keep this separate as so much more critical information
        XElement allMarketsElem = new XElement("Markets");
        rootPFS.Add(allMarketsElem);

        foreach (KeyValuePair<MarketId, MarketUserInfo> mc in _configs)
        {
            XElement mcElem = new XElement(mc.Key.ToString());

            mcElem.SetAttributeValue("Active", mc.Value.Active);

            if (string.IsNullOrWhiteSpace(mc.Value.HolidaysStorageFormat) == false)
                mcElem.SetAttributeValue("Holidays", mc.Value.HolidaysStorageFormat);

            if (mc.Value.MinFetchMins > 0)
                mcElem.SetAttributeValue("Min", mc.Value.MinFetchMins);

            allMarketsElem.Add(mcElem);
        }

        return rootPFS.ToString();
    }

    protected Dictionary<MarketId, MarketUserInfo> ImportXml(string xml)
    {
        XDocument xmlDoc = XDocument.Parse(xml);
        XElement rootPFS = xmlDoc.Element("PFS");

        Dictionary<MarketId, MarketUserInfo> ret = new();

        XElement allMarketsElem = rootPFS.Element("Markets");
        if (allMarketsElem != null && allMarketsElem.HasElements)
        {
            foreach (XElement mcElem in allMarketsElem.Elements())
            {
                MarketId marketId = (MarketId)Enum.Parse(typeof(MarketId), mcElem.Name.ToString());

                MarketUserInfo mc = new();

                if (mcElem.Attribute("Active") != null)
                    mc.Active = (bool)mcElem.Attribute("Active");

                if (mcElem.Attribute("Holidays") != null)
                    mc.HolidaysStorageFormat = (string)mcElem.Attribute("Holidays");

                if (mcElem.Attribute("Min") != null)
                    mc.MinFetchMins = (int)mcElem.Attribute("Min");

                ret.Add(marketId, mc);
            }
        }
        return ret;
    }

    public string GetCmdPrefixes() { return _componentName; }                   // ICmdHandler

    public async Task<Result<string>> CmdAsync(string cmd)                      // ICmdHandler
    {
        var parseResp = CmdParser.Parse(cmd, _cmdTemplates);

        if (parseResp.Fail) // parser gives per templates a proper fail w help
            return new FailResult<string>((parseResp as FailResult<Dictionary<string, string>>).Message);

        switch (parseResp.Data["cmd"])
        {
            case "list":
                {
                    int i = 0;
                    StringBuilder sb = new();
                    foreach (MarketDef m in _marketDef)
                    {
                        MarketId marketId = m.Market.ID;

                        string content = string.Empty;

                        if (_configs.ContainsKey(marketId))
                            content += _configs[marketId].Active ? "(A)" : "(D)";
                        else
                            content += "(d)";

                        content += $"{marketId} {m.Market.Currency} {m.Market.Name} ";

                        (DateOnly localDate, DateTime utcTime) = LastClosing(marketId);

                        content += $" LastClose={localDate.ToYMD()}";

                        if (_configs.ContainsKey(m.Market.ID))
                            content += $"[{_configs[m.Market.ID].HolidaysStorageFormat}]";

                        sb.AppendLine(content);
                        i++;
                    }

                    if ( i == 0)
                        return new OkResult<string>("--empty--");
                    else
                        return new OkResult<string>(sb.ToString());
                }
        }
        throw new NotImplementedException($"MarketConfig.CmdAsync missing {parseResp.Data["cmd"]}");
    }

    public async Task<Result<string>> HelpMeAsync(string cmd)                   // ICmdHandler
    {
        var parseResp = CmdParser.Parse(cmd, _cmdTemplates);

        if (parseResp.Fail) // parser gives per templates a proper fail w help
            return new FailResult<string>((parseResp as FailResult<Dictionary<string, string>>).Message);

        return new OkResult<string>("Cmd is OK:" + string.Join(",", parseResp.Data.Select(x => x.Key + "=" + x.Value)));
    }

    // Following table defines default information for all support market meta & closing time definations
    // https://www.tradinghours.com/markets
    // https://www.nordnet.fi/fi/markkina/porssien-aukioloajat-ja-kaupankayntikalenteri

    protected record MarketDef(MarketMeta Market, int localClosingHour, int localClosingMin, string TimezoneTag);

    protected static readonly ImmutableArray<MarketDef> _marketDef = [
        new MarketDef(new MarketMeta(MarketId.NASDAQ,   "XNAS",  "New York NASDAQ",         CurrencyId.USD), 16, 0,  "America/New_York"),   // 0930-1600 (EDT) -> 1630-2300
        new MarketDef(new MarketMeta(MarketId.NYSE,     "XNYS",  "New York Stock Exchange", CurrencyId.USD), 16, 0,  "America/New_York"),   // 0930-1600 (EDT)
        new MarketDef(new MarketMeta(MarketId.AMEX,     "XNYS",  "American Stock Exchange", CurrencyId.USD), 16, 0,  "America/New_York"),
        new MarketDef(new MarketMeta(MarketId.TSX,      "XTSE",  "Toronto Stock Exchange",  CurrencyId.CAD), 16, 0,  "America/Toronto"),    // 0930-1600 (EDT) -> 1630-2300
        new MarketDef(new MarketMeta(MarketId.OMXH,     "XHEL",  "Helsinki Stock Exchange", CurrencyId.EUR), 18, 30, "Europe/Helsinki"),    // 1000-1830
        new MarketDef(new MarketMeta(MarketId.OMX,      "XSTO",  "Stockholm Stock Exchange",CurrencyId.SEK), 17, 30, "Europe/Stockholm"),   // 0900-1730       -> 1000-1830
        new MarketDef(new MarketMeta(MarketId.LSE,      "XLON",  "London Stock Exchange",   CurrencyId.GBP), 16, 30, "Europe/London"),      // 0800-1630 (BST) -> 1000-1830
        new MarketDef(new MarketMeta(MarketId.XETRA,    "XETRA", "Deutsche Börse Xetra",    CurrencyId.EUR), 17, 30, "Europe/Berlin"),      // 0900-1730       -> 1000-1830
    ];
}
