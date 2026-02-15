using Pfs.Data.Stalker;
using Pfs.Reports;
using Pfs.Types;
using PfsReports.Tests.Helpers;
using Xunit;

namespace PfsReports.Tests.Tests;

public class WeightReportTests
{
    [Fact]
    public void Weight_WithHoldings_ReturnsHeaderAndEntries()
    {
        var stalker = ReportTestFixture.CreateStalker();
        var preCalc = ReportTestFixture.CreatePreCalc(stalker);
        var stockMeta = ReportTestFixture.CreateStockMeta();
        var stockNotes = ReportTestFixture.CreateStockNotes();
        var pfsStatus = ReportTestFixture.CreatePfsStatus();
        var filters = ReportTestFixture.CreateReportFilters();
        var today = new DateOnly(2026, 2, 15);

        var (header, stocks) = RepGenWeight.GenerateReport(today, filters, preCalc, stockMeta, stalker, stockNotes, pfsStatus);

        Assert.NotNull(header);
        Assert.NotNull(stocks);
        // Fixture has holdings for: MSFT (Main), KO (Main), NVDA (Growth), ENB (Dividend)
        Assert.True(stocks.Count >= 3, "Should have at least 3 stocks with holdings and EOD");
    }

    [Fact]
    public void Weight_CurrentP_SumsCorrectly()
    {
        var stalker = ReportTestFixture.CreateStalker();
        var preCalc = ReportTestFixture.CreatePreCalc(stalker);
        var stockMeta = ReportTestFixture.CreateStockMeta();
        var stockNotes = ReportTestFixture.CreateStockNotes();
        var pfsStatus = ReportTestFixture.CreatePfsStatus();
        var filters = ReportTestFixture.CreateReportFilters();
        var today = new DateOnly(2026, 2, 15);

        var (header, stocks) = RepGenWeight.GenerateReport(today, filters, preCalc, stockMeta, stalker, stockNotes, pfsStatus);

        // When IOwn < 1000, totalOwning = hcTotalValuation, so CurrentP should sum to ~100%
        decimal totalCurrentP = stocks.Sum(s => s.CurrentP);
        Assert.InRange(totalCurrentP, 99.9m, 100.1m);
        Assert.Equal(totalCurrentP, header.TotalCurrentP);
    }

    [Fact]
    public void Weight_TargetFromSector_Parsed()
    {
        // Set up a Weight sector with a target value
        var stalker = StalkerTestFixture.CreatePopulated();
        stalker.DoAction("Set-Sector SectorId=2 SectorName=Weight");
        stalker.DoAction("Edit-Sector SectorId=2 FieldId=0 FieldName=5%");
        stalker.DoAction("Follow-Sector SRef=NASDAQ$MSFT SectorId=2 FieldId=0");

        var preCalc = ReportTestFixture.CreatePreCalc(stalker);
        var stockMeta = ReportTestFixture.CreateStockMeta();
        var stockNotes = ReportTestFixture.CreateStockNotes();
        var pfsStatus = ReportTestFixture.CreatePfsStatus();
        var filters = ReportTestFixture.CreateReportFilters();
        var today = new DateOnly(2026, 2, 15);

        var (header, stocks) = RepGenWeight.GenerateReport(today, filters, preCalc, stockMeta, stalker, stockNotes, pfsStatus);

        var msft = stocks.FirstOrDefault(s => s.StockMeta.symbol == "MSFT");
        Assert.NotNull(msft);
        Assert.Equal("5%", msft.TargetP);
    }

    [Fact]
    public void Weight_AvrgTimeAsMonths_Correct()
    {
        var stalker = ReportTestFixture.CreateStalker();
        var preCalc = ReportTestFixture.CreatePreCalc(stalker);
        var stockMeta = ReportTestFixture.CreateStockMeta();
        var stockNotes = ReportTestFixture.CreateStockNotes();
        var pfsStatus = ReportTestFixture.CreatePfsStatus();
        var filters = ReportTestFixture.CreateReportFilters();
        var today = new DateOnly(2026, 2, 15);

        var (_, stocks) = RepGenWeight.GenerateReport(today, filters, preCalc, stockMeta, stalker, stockNotes, pfsStatus);

        // KO: bought 2022-06-10, 50 units. Days from 2022-06-10 to 2026-02-15 ≈ 1346
        // AvrgTimeAsMonths = (1346*50/50) / 30.437 ≈ 44.2
        var ko = stocks.FirstOrDefault(s => s.StockMeta.symbol == "KO");
        Assert.NotNull(ko);
        Assert.InRange(ko.AvrgTimeAsMonths, 40m, 48m);
    }

    [Fact]
    public void Weight_TradeProfits_AccumulatedCorrectly()
    {
        var stalker = ReportTestFixture.CreateStalker();
        var preCalc = ReportTestFixture.CreatePreCalc(stalker);
        var stockMeta = ReportTestFixture.CreateStockMeta();
        var stockNotes = ReportTestFixture.CreateStockNotes();
        var pfsStatus = ReportTestFixture.CreatePfsStatus();
        var filters = ReportTestFixture.CreateReportFilters();
        var today = new DateOnly(2026, 2, 15);

        var (_, stocks) = RepGenWeight.GenerateReport(today, filters, preCalc, stockMeta, stalker, stockNotes, pfsStatus);

        // NVDA has 1 trade: sold 5u@$450-1.99fee, bought@$148.50+0.4975fee, CR buy=1.34 sell=1.36
        var nvda = stocks.FirstOrDefault(s => s.StockMeta.symbol == "NVDA");
        Assert.NotNull(nvda);

        // HcTradeProfits = (HcSoldPriceWithFee - HcBuyPriceWithFee) * Units
        decimal hcSold = (450m - 1.99m) * 1.36m;
        decimal hcBuy = (148.50m + 0.4975m) * 1.34m;
        decimal expectedProfit = (hcSold - hcBuy) * 5m;
        Assert.Equal(expectedProfit, nvda.HcTradeProfits);
        Assert.True(nvda.HcTradeProfits > 0, "NVDA trade should be profitable");
    }

    [Fact]
    public void Weight_TakenAgainstValuation_Formula()
    {
        var stalker = ReportTestFixture.CreateStalker();
        var preCalc = ReportTestFixture.CreatePreCalc(stalker);
        var stockMeta = ReportTestFixture.CreateStockMeta();
        var stockNotes = ReportTestFixture.CreateStockNotes();
        var pfsStatus = ReportTestFixture.CreatePfsStatus();
        var filters = ReportTestFixture.CreateReportFilters();
        var today = new DateOnly(2026, 2, 15);

        var (_, stocks) = RepGenWeight.GenerateReport(today, filters, preCalc, stockMeta, stalker, stockNotes, pfsStatus);

        foreach (var stock in stocks)
        {
            decimal totalTaken = stock.HcHistoryDivident +
                                 (stock.RRTotalDivident?.HcDiv ?? 0) +
                                 stock.HcTradeProfits;

            if (totalTaken != 0 && stock.RCTotalHold.HcValuation > 0)
            {
                decimal expectedTakenAgainstVal = totalTaken / stock.RCTotalHold.HcValuation;
                Assert.Equal(expectedTakenAgainstVal, stock.HcTakenAgainstVal);
            }
        }
    }
}
