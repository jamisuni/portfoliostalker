namespace Pfs.Types;

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

    FMPDayCredits,
    FMPMonthCredits,
    FMPSpeedSecs,

    //TiingoDayCredits,
    //TiingoMonthCredits,
    //TiingoSpeedSecs,

    EodHDDayCredits,
    EodHDMonthCredits,
    EodHDSpeedSecs,

    ExtraColumn0, 
    ExtraColumn1, 
    ExtraColumn2, 
    ExtraColumn3,

    HideCompanyName,

    OverviewStockAmount,

    HoldingLvlPeriod,           // how many days of opposite needs to be to trigget holdingLvl break events (0=disabled, 1-15 days)

    DefTrailingSellP,           // what % is proposed by default as sell trigger

    DefTrailingBuyP,            // what % is proposed by default as buy trigger

    UseBetaFeatures,

    IOwn,                       // Effects to Weight report allowing user to define absolute own ownings total reference value
}

public static class AppCfgLimit
{
    public static int HoldingLvlPeriodMin = 0;
    public static int HoldingLvlPeriodMax = 15;
}