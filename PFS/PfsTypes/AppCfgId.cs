﻿namespace Pfs.Types;

public enum AppCfgId : int
{
    AlphaVantageDayCredits = 1,
    AlphaVantageMonthCredits,
    AlphaVantageSpeedSecs,

    PolygonDayCredits,
    PolygonMonthCredits,
    PolygonSpeedSecs,

    TwelveDataDayCredits,
    TwelveDataMonthCredits,
    TwelveDataSpeedSecs,

    UnibitDayCredits,
    UnibitMonthCredits,
    //UnibitSpeedSecs, (no limits)

    MarketstackDayCredits,
    MarketstackMonthCredits,
    //MarketstackSpeedSecs,

    //IexcloudDayCredits,   postponed
    //IexcloudMonthCredits,
    //IexcloudSpeedSecs,

    //TiingoDayCredits,
    //TiingoMonthCredits,
    //TiingoSpeedSecs,

    ExtraColumn0, 
    ExtraColumn1, 
    ExtraColumn2, 
    ExtraColumn3,
}
