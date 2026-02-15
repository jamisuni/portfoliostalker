using Pfs.Data.Stalker;
using Pfs.Reports;
using Pfs.Types;
using PfsReports.Tests.Helpers;
using Xunit;

namespace PfsReports.Tests.Tests;

public class PreCalcTests
{
    [Fact]
    public void PreCalc_AllStocks_Aggregated()
    {
        var stalker = ReportTestFixture.CreateStalker();
        var preCalc = ReportTestFixture.CreatePreCalc(stalker);
        var filters = ReportTestFixture.CreateReportFilters();

        var stocks = preCalc.GetStocks(filters, stalker).ToList();

        // All 5 fixture stocks across all portfolios should be aggregated
        Assert.True(stocks.Count >= 5, $"Expected at least 5 stocks, got {stocks.Count}");

        var sRefs = stocks.Select(s => s.Stock.SRef).ToList();
        Assert.Contains("NASDAQ$MSFT", sRefs);
        Assert.Contains("NYSE$KO", sRefs);
        Assert.Contains("NASDAQ$NVDA", sRefs);
        Assert.Contains("TSX$ENB", sRefs);
        Assert.Contains("XETRA$SAP", sRefs);
    }

    [Fact]
    public void PreCalc_FilterByPf_LimitsStocks()
    {
        var stalker = ReportTestFixture.CreateStalker();

        // Create PreCalc with PF filter for "Growth" only
        var pfFilter = new FilterByPfReportFilters("Growth");
        var preCalc = ReportTestFixture.CreatePreCalcWithFilters(pfFilter, stalker);
        var filters = ReportTestFixture.CreateReportFilters();

        var stocks = preCalc.GetStocks(filters, stalker).ToList();

        // Growth portfolio has: NVDA, SAP
        Assert.Equal(2, stocks.Count);
        var sRefs = stocks.Select(s => s.Stock.SRef).ToList();
        Assert.Contains("NASDAQ$NVDA", sRefs);
        Assert.Contains("XETRA$SAP", sRefs);
    }

    [Fact]
    public void PreCalc_FilterBySector_Excludes()
    {
        var stalker = ReportTestFixture.CreateStalker();
        var preCalc = ReportTestFixture.CreatePreCalc(stalker);

        // Filter sector 0 (Industry) to only "Tech"
        var sectorFilter = new FilterBySectorReportFilters(0, "Tech");

        var stocks = preCalc.GetStocks(sectorFilter, stalker).ToList();

        // Only MSFT and NVDA have Industry=Tech
        var sRefs = stocks.Select(s => s.Stock.SRef).ToList();
        Assert.Contains("NASDAQ$MSFT", sRefs);
        Assert.Contains("NASDAQ$NVDA", sRefs);
        Assert.DoesNotContain("NYSE$KO", sRefs);   // Consumer
        Assert.DoesNotContain("TSX$ENB", sRefs);   // Energy
    }

    [Fact]
    public void PreCalc_FilterByOwning_Holding()
    {
        var stalker = ReportTestFixture.CreateStalker();
        var preCalc = ReportTestFixture.CreatePreCalc(stalker);

        // Filter to only Holding stocks (those with current holdings)
        var owningFilter = new FilterByOwningReportFilters(ReportOwningFilter.Holding);

        var stocks = preCalc.GetStocks(owningFilter, stalker).ToList();

        // All returned stocks should have holdings
        Assert.All(stocks, s => Assert.True(s.Holdings.Count > 0, $"{s.Stock.SRef} should have holdings"));

        // SAP has no holdings (tracking only in Growth) — should be excluded
        Assert.DoesNotContain(stocks, s => s.Stock.SRef == "XETRA$SAP");
    }

    [Fact]
    public void PreCalc_RecalculateTotals_Correct()
    {
        var stalker = ReportTestFixture.CreateStalker();
        var preCalc = ReportTestFixture.CreatePreCalc(stalker);
        var filters = ReportTestFixture.CreateReportFilters();

        var stocks = preCalc.GetStocks(filters, stalker).ToList();

        // MSFT: 2 holdings (M001: 10u, M002: 5u) → total 15u
        var msft = stocks.First(s => s.Stock.SRef == "NASDAQ$MSFT");
        Assert.NotNull(msft.RCTotalHold);
        Assert.Equal(15m, msft.RCTotalHold.Units);

        // HcValuation = (int)(HcClosePrice * Units) = (int)(420 * 0.92 * 15)
        Assert.Equal((int)(420m * 0.92m * 15m), msft.RCTotalHold.HcValuation);

        // ENB: 2 holdings (E001: 100u, E002: 50u) → total 150u
        var enb = stocks.First(s => s.Stock.SRef == "TSX$ENB");
        Assert.NotNull(enb.RCTotalHold);
        Assert.Equal(150m, enb.RCTotalHold.Units);
    }

    [Fact]
    public void PreCalc_OrdersClosestTrigger_Kept()
    {
        // Add two orders for MSFT in Main — should keep only closest to trigger
        var stalker = StalkerTestFixture.CreatePopulated();
        // Existing: Buy 5@250 (distance from close 420 is far)
        // Add another closer one:
        stalker.DoAction("Add-Order PfName=Main Type=Buy SRef=NASDAQ$MSFT Units=3 Price=400.00 LastDate=2026-12-31");

        var preCalc = ReportTestFixture.CreatePreCalc(stalker);
        var filters = ReportTestFixture.CreateReportFilters();

        var stocks = preCalc.GetStocks(filters, stalker).ToList();
        var msft = stocks.First(s => s.Stock.SRef == "NASDAQ$MSFT");

        // Should have exactly 1 order for Main PF (the closest to trigger)
        var mainOrders = msft.Orders.Where(o => o.PfName == "Main").ToList();
        Assert.Single(mainOrders);

        // The kept order should be the one closer to current price (400 is closer to 420 than 250)
        Assert.Equal(400m, mainOrders[0].SO.PricePerUnit);
    }
}
