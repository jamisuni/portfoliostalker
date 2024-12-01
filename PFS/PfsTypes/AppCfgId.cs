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
}

public static class AppCfgLimit
{
    public static int HoldingLvlPeriodMin = 0;
    public static int HoldingLvlPeriodMax = 15;
}