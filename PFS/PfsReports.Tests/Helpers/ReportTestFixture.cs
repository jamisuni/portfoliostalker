using Pfs.Data.Stalker;
using Pfs.Reports;
using Pfs.Types;

namespace PfsReports.Tests.Helpers;

// Provides a complete test environment matching StalkerTestFixture.CreatePopulated()
// with EOD prices, StockMeta, conversion rates for all fixture stocks.
//
// Fixture stocks:
//   NASDAQ$MSFT  - Main PF:  M001(10u@280.50+0.995fee, CR=1.35), M002(5u@330.00+0.99fee, CR=1.36)
//   NYSE$KO      - Main PF:  K001(50u@62.50+0.199fee, CR=1.32), 3 quarterly dividends @0.485
//   NASDAQ$NVDA  - Growth PF: N001(20u@148.50+0.4975fee, CR=1.34), partial sale 5u@450.00 T001
//   TSX$ENB      - Dividend PF: E001(100u@54.20+0fee, CR=1), E002(50u@48.75+0fee, CR=1), 1 div @0.915
//   XETRA$SAP    - Growth PF: tracked only, no holdings
//   TSX$RY       - not in fixture portfolios
//
// EOD prices (market currency): MSFT=$420, KO=$62, NVDA=$880, ENB=CAD$52, SAP=EUR€195
// Conversion rates to EUR (home currency): USD=0.92, CAD=0.68, EUR=1.0
// EOD date: 2026-02-14
public static class ReportTestFixture
{
    public static readonly DateOnly EodDate = new DateOnly(2026, 2, 14);

    // Conversion rates
    public const decimal RateUSD = 0.92m;
    public const decimal RateCAD = 0.68m;
    public const decimal RateEUR = 1.0m;

    // EOD close prices (market currency)
    public const decimal MsftClose = 420m;
    public const decimal KoClose = 62m;
    public const decimal NvdaClose = 880m;
    public const decimal EnbClose = 52m;
    public const decimal SapClose = 195m;

    // PrevClose prices
    public const decimal MsftPrevClose = 415m;
    public const decimal KoPrevClose = 61.50m;
    public const decimal NvdaPrevClose = 870m;
    public const decimal EnbPrevClose = 51.50m;
    public const decimal SapPrevClose = 193m;

    public static StalkerDoCmd CreateStalker() => StalkerTestFixture.CreatePopulated();

    public static StubStockMeta CreateStockMeta()
    {
        return new StubStockMeta()
            .Add(MarketId.NASDAQ, "MSFT", "Microsoft Corp", CurrencyId.USD)
            .Add(MarketId.NYSE, "KO", "Coca-Cola Co", CurrencyId.USD)
            .Add(MarketId.NASDAQ, "NVDA", "NVIDIA Corp", CurrencyId.USD)
            .Add(MarketId.TSX, "ENB", "Enbridge Inc", CurrencyId.CAD)
            .Add(MarketId.XETRA, "SAP", "SAP SE", CurrencyId.EUR);
    }

    public static StubEodLatest CreateEodLatest()
    {
        return new StubEodLatest()
            .Add("NASDAQ$MSFT", MsftClose, MsftPrevClose, EodDate)
            .Add("NYSE$KO", KoClose, KoPrevClose, EodDate)
            .Add("NASDAQ$NVDA", NvdaClose, NvdaPrevClose, EodDate)
            .Add("TSX$ENB", EnbClose, EnbPrevClose, EodDate)
            .Add("XETRA$SAP", SapClose, SapPrevClose, EodDate);
    }

    public static StubLatestRates CreateLatestRates()
    {
        return new StubLatestRates()
            .Add(CurrencyId.USD, RateUSD)
            .Add(CurrencyId.CAD, RateCAD)
            .Add(CurrencyId.EUR, RateEUR);
    }

    public static StubMarketMeta CreateMarketMeta()
    {
        return new StubMarketMeta()
            .Add(MarketId.NASDAQ, "XNAS", "NASDAQ", CurrencyId.USD, EodDate)
            .Add(MarketId.NYSE, "XNYS", "NYSE", CurrencyId.USD, EodDate)
            .Add(MarketId.TSX, "XTSE", "TSX", CurrencyId.CAD, EodDate)
            .Add(MarketId.XETRA, "XETR", "XETRA", CurrencyId.EUR, EodDate);
    }

    public static StubPfsPlatform CreatePfsPlatform() => new();
    public static StubStockNotes CreateStockNotes() => new();
    public static StubPfsStatus CreatePfsStatus() => new();
    public static StubReportFilters CreateReportFilters() => new();
    public static StubExtraColumns CreateExtraColumns() => new();
    public static StubFetchConfig CreateFetchConfig() => new();

    // Create ReportPreCalc with all stubs wired together
    public static ReportPreCalc CreatePreCalc(StalkerData stalkerData = null)
    {
        var stalker = stalkerData ?? CreateStalker();
        return new ReportPreCalc(
            limitSinglePfName: string.Empty,
            reportFilters: CreateReportFilters(),
            pfsPlatform: CreatePfsPlatform(),
            latestEodProv: CreateEodLatest(),
            stockMetaProv: CreateStockMeta(),
            marketMetaProv: CreateMarketMeta(),
            latestRatesProv: CreateLatestRates(),
            stalkerData: stalker
        );
    }

    // Create ReportPreCalc limited to a single portfolio
    public static ReportPreCalc CreatePreCalcForPf(string pfName, StalkerData stalkerData = null)
    {
        var stalker = stalkerData ?? CreateStalker();
        return new ReportPreCalc(
            limitSinglePfName: pfName,
            reportFilters: CreateReportFilters(),
            pfsPlatform: CreatePfsPlatform(),
            latestEodProv: CreateEodLatest(),
            stockMetaProv: CreateStockMeta(),
            marketMetaProv: CreateMarketMeta(),
            latestRatesProv: CreateLatestRates(),
            stalkerData: stalker
        );
    }

    // Create ReportPreCalc with custom report filters
    public static ReportPreCalc CreatePreCalcWithFilters(IReportFilters filters, StalkerData stalkerData = null)
    {
        var stalker = stalkerData ?? CreateStalker();
        return new ReportPreCalc(
            limitSinglePfName: string.Empty,
            reportFilters: filters,
            pfsPlatform: CreatePfsPlatform(),
            latestEodProv: CreateEodLatest(),
            stockMetaProv: CreateStockMeta(),
            marketMetaProv: CreateMarketMeta(),
            latestRatesProv: CreateLatestRates(),
            stalkerData: stalker
        );
    }
}
