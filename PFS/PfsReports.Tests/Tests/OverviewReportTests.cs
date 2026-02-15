using Pfs.Data.Stalker;
using Pfs.Reports;
using Pfs.Types;
using PfsReports.Tests.Helpers;
using Xunit;

namespace PfsReports.Tests.Tests;

public class OverviewReportTests
{
    // ==================== OverviewGroups Tests ====================

    [Fact]
    public void OverviewGroups_DefaultGroups_Created()
    {
        var stalker = ReportTestFixture.CreateStalker();
        var preCalc = ReportTestFixture.CreatePreCalc(stalker);
        var pfsStatus = ReportTestFixture.CreatePfsStatus();
        var rates = ReportTestFixture.CreateLatestRates();
        var filters = ReportTestFixture.CreateReportFilters();

        var result = ReportOverviewGroups.GenerateReport(filters, preCalc, pfsStatus, stalker, rates);
        Assert.True(result.Ok);

        var groups = ((OkResult<List<OverviewGroupsData>>)result).Data;
        var names = groups.Select(g => g.Name).ToList();

        // 5 default groups
        Assert.Contains("All Alarms", names);
        Assert.Contains("Investments", names);
        Assert.Contains("Top Valuations", names);
        Assert.Contains("Oldies", names);
        Assert.Contains("Tracking's", names);

        // Plus per-portfolio groups: Main, Growth, Dividend
        Assert.Contains("PF: Main", names);
        Assert.Contains("PF: Growth", names);
        Assert.Contains("PF: Dividend", names);

        Assert.Equal(8, groups.Count); // 5 default + 3 PFs
    }

    [Fact]
    public void OverviewGroups_HcTotalValuation_Correct()
    {
        var stalker = ReportTestFixture.CreateStalker();
        var preCalc = ReportTestFixture.CreatePreCalc(stalker);
        var pfsStatus = ReportTestFixture.CreatePfsStatus();
        var rates = ReportTestFixture.CreateLatestRates();
        var filters = ReportTestFixture.CreateReportFilters();

        var result = ReportOverviewGroups.GenerateReport(filters, preCalc, pfsStatus, stalker, rates);
        var groups = ((OkResult<List<OverviewGroupsData>>)result).Data;

        var investments = groups.First(g => g.Name == "Investments");

        // All holding stocks should be in Investments group
        Assert.True(investments.HcTotalValuation > 0);
        Assert.True(investments.HcTotalInvested > 0);

        // GrowthP formula: (Val - Inv) / Inv * 100, rounded to 1 decimal
        if (investments.HcTotalInvested > 0)
        {
            decimal expectedGrowthP = decimal.Round(
                (investments.HcTotalValuation - investments.HcTotalInvested) / investments.HcTotalInvested * 100, 1);
            Assert.Equal(expectedGrowthP, investments.HcGrowthP);
        }
    }

    // ==================== OverviewStocks Tests ====================

    [Fact]
    public void OverviewStocks_ActiveMarkets_Only()
    {
        // Create a stock on CLOSED market and verify it's excluded
        var stalker = StalkerTestFixture.CreatePopulated();
        var stockMeta = new StubStockMeta()
            .Add(MarketId.NASDAQ, "MSFT", "Microsoft Corp", CurrencyId.USD)
            .Add(MarketId.NYSE, "KO", "Coca-Cola Co", CurrencyId.USD)
            .Add(MarketId.NASDAQ, "NVDA", "NVIDIA Corp", CurrencyId.USD)
            .Add(MarketId.TSX, "ENB", "Enbridge Inc", CurrencyId.CAD)
            .Add(MarketId.XETRA, "SAP", "SAP SE", CurrencyId.EUR)
            .Add(MarketId.CLOSED, "OLD", "Old Stock", CurrencyId.USD);

        var eod = ReportTestFixture.CreateEodLatest();
        // Add EOD for CLOSED stock
        eod.Add("CLOSED$OLD", 10m, 9.5m, ReportTestFixture.EodDate);

        var marketMeta = ReportTestFixture.CreateMarketMeta();
        // Note: CLOSED market is not in active markets list

        var preCalc = new ReportPreCalc(
            limitSinglePfName: string.Empty,
            reportFilters: ReportTestFixture.CreateReportFilters(),
            pfsPlatform: ReportTestFixture.CreatePfsPlatform(),
            latestEodProv: eod,
            stockMetaProv: stockMeta,
            marketMetaProv: marketMeta,
            latestRatesProv: ReportTestFixture.CreateLatestRates(),
            stalkerData: stalker
        );

        var pfsStatus = ReportTestFixture.CreatePfsStatus();
        var stockNotes = ReportTestFixture.CreateStockNotes();
        var extraColumns = ReportTestFixture.CreateExtraColumns();
        var filters = ReportTestFixture.CreateReportFilters();

        var result = ReportOverviewStocks.GenerateReport(filters, preCalc, pfsStatus, stalker, stockMeta, marketMeta, extraColumns, stockNotes);

        // No CLOSED market stocks should appear
        Assert.DoesNotContain(result, s => s.StockMeta.marketId == MarketId.CLOSED);
    }

    [Fact]
    public void OverviewStocks_AllStocks_Returned()
    {
        var stalker = ReportTestFixture.CreateStalker();
        var preCalc = ReportTestFixture.CreatePreCalc(stalker);
        var pfsStatus = ReportTestFixture.CreatePfsStatus();
        var stockMeta = ReportTestFixture.CreateStockMeta();
        var marketMeta = ReportTestFixture.CreateMarketMeta();
        var extraColumns = ReportTestFixture.CreateExtraColumns();
        var stockNotes = ReportTestFixture.CreateStockNotes();
        var filters = ReportTestFixture.CreateReportFilters();

        var result = ReportOverviewStocks.GenerateReport(filters, preCalc, pfsStatus, stalker, stockMeta, marketMeta, extraColumns, stockNotes);

        Assert.NotNull(result);
        Assert.True(result.Count >= 4, "Should have at least 4 fixture stocks with EOD");

        var symbols = result.Select(s => s.StockMeta.symbol).ToList();
        Assert.Contains("MSFT", symbols);
        Assert.Contains("KO", symbols);
        Assert.Contains("NVDA", symbols);
        Assert.Contains("ENB", symbols);
        Assert.Contains("SAP", symbols);
    }

    [Fact]
    public void OverviewStocks_AlarmsOrders_Populated()
    {
        var stalker = ReportTestFixture.CreateStalker();
        var preCalc = ReportTestFixture.CreatePreCalc(stalker);
        var pfsStatus = ReportTestFixture.CreatePfsStatus();
        var stockMeta = ReportTestFixture.CreateStockMeta();
        var marketMeta = ReportTestFixture.CreateMarketMeta();
        var extraColumns = ReportTestFixture.CreateExtraColumns();
        var stockNotes = ReportTestFixture.CreateStockNotes();
        var filters = ReportTestFixture.CreateReportFilters();

        var result = ReportOverviewStocks.GenerateReport(filters, preCalc, pfsStatus, stalker, stockMeta, marketMeta, extraColumns, stockNotes);

        // MSFT has alarm Under 250
        var msft = result.FirstOrDefault(s => s.StockMeta.symbol == "MSFT");
        Assert.NotNull(msft);
        Assert.NotNull(msft.RRAlarm);
        Assert.Equal(250m, msft.RRAlarm.Under);

        // NVDA has alarm Over 800
        var nvda = result.FirstOrDefault(s => s.StockMeta.symbol == "NVDA");
        Assert.NotNull(nvda);
        Assert.NotNull(nvda.RRAlarm);
        Assert.Equal(800m, nvda.RRAlarm.Over);

        // MSFT has order from Main (Buy 5@250)
        Assert.True(msft.PfOrder.Count > 0, "MSFT should have orders");

        // NVDA has order from Growth (Sell 10@900)
        Assert.True(nvda.PfOrder.Count > 0, "NVDA should have orders");
    }
}
