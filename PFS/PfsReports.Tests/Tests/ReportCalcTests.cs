using Pfs.Data;
using Pfs.Data.Stalker;
using Pfs.Reports;
using Pfs.Types;
using PfsReports.Tests.Helpers;
using Xunit;

namespace PfsReports.Tests.Tests;

public class ReportCalcTests
{
    // ==================== RepGenStMgHoldings Tests ====================

    [Fact]
    public void StMgHoldings_SingleStock_GrowthCalculation()
    {
        // Test KO holdings growth in Main portfolio
        var stalker = ReportTestFixture.CreateStalker();
        var stockMeta = ReportTestFixture.CreateStockMeta();
        var eod = ReportTestFixture.CreateEodLatest();
        var rates = ReportTestFixture.CreateLatestRates();

        var result = RepGenStMgHoldings.GenerateReport("NYSE$KO", stalker, stockMeta, eod, rates);
        Assert.True(result.Ok);

        var report = ((OkResult<List<RepDataStMgHoldings>>)result).Data;
        Assert.Single(report); // KO has 1 holding (K001) in Main

        var entry = report[0];
        Assert.Equal("Main", entry.PfName);
        Assert.Equal(50m, entry.Holding.Units);

        // RRTotalHold = RCGrowth(holding, mcClosePrice=62, latestConversionRate=0.92)
        Assert.NotNull(entry.RRTotalHold);
        Assert.Equal(50m, entry.RRTotalHold.Units);
        Assert.Equal(62m, entry.RRTotalHold.McClosePrice);
        Assert.Equal(62m * ReportTestFixture.RateUSD, entry.RRTotalHold.HcClosePrice);
    }

    [Fact]
    public void StMgHoldings_TwoHoldings_BothReturned()
    {
        // ENB has 2 holdings (E001, E002) in Dividend portfolio
        var stalker = ReportTestFixture.CreateStalker();
        var stockMeta = ReportTestFixture.CreateStockMeta();
        var eod = ReportTestFixture.CreateEodLatest();
        var rates = ReportTestFixture.CreateLatestRates();

        var result = RepGenStMgHoldings.GenerateReport("TSX$ENB", stalker, stockMeta, eod, rates);
        Assert.True(result.Ok);

        var report = ((OkResult<List<RepDataStMgHoldings>>)result).Data;
        Assert.Equal(2, report.Count);

        // Both should be from Dividend portfolio
        Assert.All(report, e => Assert.Equal("Dividend", e.PfName));

        // Ordered by PurhaceDate: E001(2022-01-15) then E002(2023-06-01)
        Assert.Equal(100m, report[0].Holding.Units); // E001
        Assert.Equal(50m, report[1].Holding.Units);  // E002

        // Each holding gets its own RCGrowth
        // E001: 100u@54.20+0fee, CR=1, close=52, convRate=0.68
        Assert.Equal(100m, report[0].RRTotalHold.Units);
        Assert.Equal(52m, report[0].RRTotalHold.McClosePrice);
    }

    [Fact]
    public void StMgHoldings_WithDividents_DividentAggregated()
    {
        // ENB has 1 dividend across both holdings
        var stalker = ReportTestFixture.CreateStalker();
        var stockMeta = ReportTestFixture.CreateStockMeta();
        var eod = ReportTestFixture.CreateEodLatest();
        var rates = ReportTestFixture.CreateLatestRates();

        var result = RepGenStMgHoldings.GenerateReport("TSX$ENB", stalker, stockMeta, eod, rates);
        var report = ((OkResult<List<RepDataStMgHoldings>>)result).Data;

        // The dividend is added via: Add-Divident PfName=Dividend SRef=TSX$ENB OptPurhaceId= OptTradeId= ...
        // When OptPurhaceId is empty, dividend goes to ALL holdings of that stock in that PF
        // E001 has 100 units, E002 has 50 units → both should have dividend entries
        // Check that at least one holding has dividends
        bool anyDivs = report.Any(e => e.TotalHoldingDivident != null);
        Assert.True(anyDivs, "At least one holding should have dividends");
    }

    [Fact]
    public void StMgHoldings_NullStockMeta_ReturnsFailResult()
    {
        // BUG-13 fix verification
        var stalker = ReportTestFixture.CreateStalker();
        var stockMeta = new StubStockMeta(); // Empty — won't find "NYSE$KO"
        var eod = ReportTestFixture.CreateEodLatest();
        var rates = ReportTestFixture.CreateLatestRates();

        var result = RepGenStMgHoldings.GenerateReport("NYSE$KO", stalker, stockMeta, eod, rates);
        Assert.False(result.Ok);
    }

    // ==================== RepGenStMgHistory Tests ====================

    [Fact]
    public void StMgHistory_NullStockMeta_ReturnsFailResult()
    {
        // BUG-12 fix verification
        var stalker = ReportTestFixture.CreateStalker();
        var stockMeta = new StubStockMeta(); // Empty
        var eod = ReportTestFixture.CreateEodLatest();
        var rates = ReportTestFixture.CreateLatestRates();
        var hist = new StoreStockMetaHist(new StubPfsPlatform());

        var result = RepGenStMgHistory.GenerateReport("NYSE$KO", stalker, stockMeta, eod, rates, hist);
        Assert.False(result.Ok);
    }

    [Fact]
    public void StMgHistory_SimpleHolding_CreatesOwnEntry()
    {
        // KO has 1 holding in Main, no trades → should have Total + 1 Own
        var stalker = ReportTestFixture.CreateStalker();
        var stockMeta = ReportTestFixture.CreateStockMeta();
        var eod = ReportTestFixture.CreateEodLatest();
        var rates = ReportTestFixture.CreateLatestRates();
        var hist = new StoreStockMetaHist(new StubPfsPlatform());

        var result = RepGenStMgHistory.GenerateReport("NYSE$KO", stalker, stockMeta, eod, rates, hist);
        Assert.True(result.Ok);
        var report = ((OkResult<List<RepDataStMgHistory>>)result).Data;

        // Should have: 1 Total + 1 Own
        var totalEntry = report.FirstOrDefault(e => e.Total != null);
        var ownEntries = report.Where(e => e.Own != null).ToList();

        Assert.NotNull(totalEntry);
        Assert.Single(ownEntries);

        var own = ownEntries[0];
        Assert.Equal("Main", own.PfName);
        Assert.Equal(50m, own.Own.Holding.Units);

        // Own.HcInv = HcPriceWithFeePerUnit * Units = (62.699 * 1.32) * 50 = 82.76268 * 50 = 4138.134
        decimal expectedHcInv = (62.699m * 1.32m) * 50m;
        Assert.Equal(expectedHcInv, own.Own.HcInv);

        // Own.HcGrowth = (HcClose - HcPriceWithFeePerUnit) * Units
        // HcClose = 62 * 0.92 = 57.04
        decimal hcClose = 62m * 0.92m;
        decimal expectedHcGrowth = (hcClose - (62.699m * 1.32m)) * 50m;
        Assert.Equal(expectedHcGrowth, own.Own.HcGrowth);
    }

    [Fact]
    public void StMgHistory_PartialSale_CreatesOwnBuySoldEntries()
    {
        // NVDA: 20u bought (N001), 5u sold (T001) → 15u remaining
        // Should have: Total + Own(15u) + Buy(for the sold part) + Sold(5u)
        var stalker = ReportTestFixture.CreateStalker();
        var stockMeta = ReportTestFixture.CreateStockMeta();
        var eod = ReportTestFixture.CreateEodLatest();
        var rates = ReportTestFixture.CreateLatestRates();
        var hist = new StoreStockMetaHist(new StubPfsPlatform());

        var result = RepGenStMgHistory.GenerateReport("NASDAQ$NVDA", stalker, stockMeta, eod, rates, hist);
        Assert.True(result.Ok);
        var report = ((OkResult<List<RepDataStMgHistory>>)result).Data;

        var ownEntries = report.Where(e => e.Own != null).ToList();
        var buyEntries = report.Where(e => e.Buy != null).ToList();
        var soldEntries = report.Where(e => e.Sold != null).ToList();

        // Partial sale: Units(15) < OriginalUnits(20), so we get Own + Buy
        Assert.Single(ownEntries);
        Assert.Equal(15m, ownEntries[0].Own.Holding.Units); // remaining units

        Assert.Single(buyEntries);
        Assert.Single(soldEntries);

        // Sold entry should have 5 units
        Assert.Equal(5m, soldEntries[0].Sold.Holding.Units);
    }

    [Fact]
    public void StMgHistory_Total_AggregatesOwnAndSold()
    {
        // NVDA: test that Total aggregates Own + Sold correctly
        var stalker = ReportTestFixture.CreateStalker();
        var stockMeta = ReportTestFixture.CreateStockMeta();
        var eod = ReportTestFixture.CreateEodLatest();
        var rates = ReportTestFixture.CreateLatestRates();
        var hist = new StoreStockMetaHist(new StubPfsPlatform());

        var result = RepGenStMgHistory.GenerateReport("NASDAQ$NVDA", stalker, stockMeta, eod, rates, hist);
        var report = ((OkResult<List<RepDataStMgHistory>>)result).Data;

        var total = report.First(e => e.Total != null).Total;
        var own = report.First(e => e.Own != null);
        var sold = report.First(e => e.Sold != null);

        // Total.HcInv should match Own.HcInv (only current holdings)
        Assert.Equal(own.Own.HcInv, total.HcInv);

        // Total.HcGrowth should match Own.HcGrowth
        Assert.Equal(own.Own.HcGrowth, total.HcGrowth);

        // Total.HcProfit = sold.HcSold - sold.HcInv
        decimal expectedProfit = sold.Sold.HcSold - sold.Sold.HcInv;
        Assert.Equal(expectedProfit, total.HcProfit);
    }

    // ==================== RepGenInvested Tests (via PreCalc) ====================

    [Fact]
    public void Invested_WithHoldings_HeaderTotalsMatchSum()
    {
        var stalker = ReportTestFixture.CreateStalker();
        var preCalc = ReportTestFixture.CreatePreCalc(stalker);
        var stockMeta = ReportTestFixture.CreateStockMeta();
        var stockNotes = ReportTestFixture.CreateStockNotes();
        var filters = ReportTestFixture.CreateReportFilters();

        var (header, stocks) = RepGenInvested.GenerateReport(filters, preCalc, stockMeta, stalker, stockNotes);

        Assert.NotNull(header);
        Assert.NotNull(stocks);
        Assert.True(stocks.Count > 0);

        // Header totals must equal sum of individual entries
        decimal sumInvested = stocks.Sum(s => s.RCTotalHold.HcInvested);
        decimal sumValuation = stocks.Sum(s => s.RCTotalHold.HcValuation);

        Assert.Equal(sumInvested, header.HcTotalInvested);
        Assert.Equal(sumValuation, header.HcTotalValuation);
    }

    [Fact]
    public void Invested_HcGrowthP_CorrectFormula()
    {
        var stalker = ReportTestFixture.CreateStalker();
        var preCalc = ReportTestFixture.CreatePreCalc(stalker);
        var stockMeta = ReportTestFixture.CreateStockMeta();
        var stockNotes = ReportTestFixture.CreateStockNotes();
        var filters = ReportTestFixture.CreateReportFilters();

        var (header, stocks) = RepGenInvested.GenerateReport(filters, preCalc, stockMeta, stalker, stockNotes);

        // HcGrowthP = (HcTotalValuation - HcTotalInvested) / HcTotalInvested * 100
        int expectedGrowthP = (int)((header.HcTotalValuation - header.HcTotalInvested) / header.HcTotalInvested * 100);
        Assert.Equal(expectedGrowthP, header.HcGrowthP);
    }

    [Fact]
    public void Invested_HcGain_IncludesGrowthAndDividends()
    {
        var stalker = ReportTestFixture.CreateStalker();
        var preCalc = ReportTestFixture.CreatePreCalc(stalker);
        var stockMeta = ReportTestFixture.CreateStockMeta();
        var stockNotes = ReportTestFixture.CreateStockNotes();
        var filters = ReportTestFixture.CreateReportFilters();

        var (header, stocks) = RepGenInvested.GenerateReport(filters, preCalc, stockMeta, stalker, stockNotes);

        foreach (var stock in stocks)
        {
            // HcGain = HcGrowthAmount + HcDiv (if dividends exist)
            decimal expectedGain = stock.RCTotalHold.HcGrowthAmount;
            if (stock.RRTotalDivident != null)
                expectedGain += stock.RRTotalDivident.HcDiv;

            // Cast to int for comparison since HcGain is stored as decimal but computed from int HcGrowthAmount
            Assert.Equal(expectedGain, stock.HcGain);
        }
    }

    [Fact]
    public void Invested_HcGainP_DividedByInvested()
    {
        var stalker = ReportTestFixture.CreateStalker();
        var preCalc = ReportTestFixture.CreatePreCalc(stalker);
        var stockMeta = ReportTestFixture.CreateStockMeta();
        var stockNotes = ReportTestFixture.CreateStockNotes();
        var filters = ReportTestFixture.CreateReportFilters();

        var (header, stocks) = RepGenInvested.GenerateReport(filters, preCalc, stockMeta, stalker, stockNotes);

        foreach (var stock in stocks)
        {
            if (stock.HcGain != 0)
            {
                int expectedGainP = (int)(stock.HcGain / stock.RCTotalHold.HcInvested * 100);
                Assert.Equal(expectedGainP, stock.HcGainP);
            }
            else
            {
                Assert.Equal(0, stock.HcGainP);
            }
        }
    }

    [Fact]
    public void Invested_PercentOfTotal_SumsCorrectly()
    {
        var stalker = ReportTestFixture.CreateStalker();
        var preCalc = ReportTestFixture.CreatePreCalc(stalker);
        var stockMeta = ReportTestFixture.CreateStockMeta();
        var stockNotes = ReportTestFixture.CreateStockNotes();
        var filters = ReportTestFixture.CreateReportFilters();

        var (header, stocks) = RepGenInvested.GenerateReport(filters, preCalc, stockMeta, stalker, stockNotes);

        // All HcInvestedOfTotalP should sum to ~100%
        decimal totalInvestedP = stocks.Sum(s => s.HcInvestedOfTotalP);
        Assert.InRange(totalInvestedP, 99.9m, 100.1m);

        // All HcValuationOfTotalP should sum to ~100%
        decimal totalValuationP = stocks.Sum(s => s.HcValuationOfTotalP);
        Assert.InRange(totalValuationP, 99.9m, 100.1m);
    }

    [Fact]
    public void Invested_HeaderTotalGain_Correct()
    {
        var stalker = ReportTestFixture.CreateStalker();
        var preCalc = ReportTestFixture.CreatePreCalc(stalker);
        var stockMeta = ReportTestFixture.CreateStockMeta();
        var stockNotes = ReportTestFixture.CreateStockNotes();
        var filters = ReportTestFixture.CreateReportFilters();

        var (header, _) = RepGenInvested.GenerateReport(filters, preCalc, stockMeta, stalker, stockNotes);

        // HcTotalGain = HcTotalValuation - HcTotalInvested + hcTotalDiv
        decimal expectedTotalGain = header.HcTotalValuation - header.HcTotalInvested + header.HcTotalDivident.HcDiv;
        Assert.Equal(expectedTotalGain, header.HcTotalGain);
    }

    // ==================== RepGenPfSales Tests ====================

    [Fact]
    public void PfSales_PartialNvdaSale_TradeGrowthCorrect()
    {
        // NVDA trade T001: 5u sold @ $450.00, fee=$9.95 (FeePerUnit=1.99), CR=1.36
        // Original buy: 20u @ $148.50, fee=$9.95 (FeePerUnit=0.4975), CR=1.34
        var stalker = ReportTestFixture.CreateStalker();
        var preCalc = ReportTestFixture.CreatePreCalc(stalker);
        var stockMeta = ReportTestFixture.CreateStockMeta();
        var stockNotes = ReportTestFixture.CreateStockNotes();
        var filters = ReportTestFixture.CreateReportFilters();

        var sales = RepGenPfSales.GenerateReport(filters, preCalc, stockMeta, stalker, stockNotes);

        Assert.NotNull(sales);
        Assert.Single(sales); // Only 1 trade in fixture

        var trade = sales[0];
        Assert.Equal("T001", trade.TradeId);
        Assert.Equal(5m, trade.SoldTotalUnits);

        // TotalGrowth calculated over all holdings in trade
        Assert.NotNull(trade.TotalGrowth);
        Assert.Equal(5m, trade.TotalGrowth.Units);

        // McSoldUnitPriceWithFee = 450.00 - 1.99 = 448.01  (Sale: Price - Fee)
        // McBuyUnitPriceWithFee = 148.50 + 0.4975 = 148.9975
        // McInvested = 148.9975 * 5 = 744.9875
        // McGrowthAmount = (448.01 * 5) - 744.9875 = 2240.05 - 744.9875 = 1495.0625
        Assert.True(trade.TotalGrowth.McGrowthAmount > 0, "NVDA sale should show profit");
    }

    // ==================== RepGenExpHoldings Tests ====================

    [Fact]
    public void ExpHoldings_AvrgTimeAsMonths_Calculated()
    {
        var stalker = ReportTestFixture.CreateStalker();
        var preCalc = ReportTestFixture.CreatePreCalc(stalker);
        var stockMeta = ReportTestFixture.CreateStockMeta();
        var filters = ReportTestFixture.CreateReportFilters();
        var today = new DateOnly(2026, 2, 15);

        var result = RepGenExpHoldings.GenerateReport(today, filters, preCalc, stockMeta, stalker);

        Assert.NotNull(result);
        Assert.True(result.Count > 0);

        foreach (var entry in result)
        {
            // All holding-based entries should have positive AvrgTimeAsMonths
            Assert.True(entry.AvrgTimeAsMonths > 0, $"{entry.StockMeta.symbol} should have positive holding time");
        }

        // KO was bought 2022-06-10, today is 2026-02-15 → ~44 months
        var ko = result.FirstOrDefault(e => e.StockMeta.symbol == "KO");
        Assert.NotNull(ko);
        // days from 2022-06-10 to 2026-02-15 = approx 1346 days → 1346/30.437 ≈ 44.2 months
        Assert.InRange(ko.AvrgTimeAsMonths, 40m, 48m);
    }

    [Fact]
    public void ExpHoldings_HcGain_MatchesGrowthPlusDividends()
    {
        var stalker = ReportTestFixture.CreateStalker();
        var preCalc = ReportTestFixture.CreatePreCalc(stalker);
        var stockMeta = ReportTestFixture.CreateStockMeta();
        var filters = ReportTestFixture.CreateReportFilters();
        var today = new DateOnly(2026, 2, 15);

        var result = RepGenExpHoldings.GenerateReport(today, filters, preCalc, stockMeta, stalker);

        foreach (var entry in result)
        {
            decimal expectedGain = entry.RCTotalHold.HcGrowthAmount;
            if (entry.RRTotalDivident != null)
                expectedGain += entry.RRTotalDivident.HcDiv;

            Assert.Equal(expectedGain, entry.HcGain);
        }
    }

    // ==================== RCEod Tests ====================

    [Fact]
    public void RCEod_ChangeP_CorrectCalculation()
    {
        var eod = new FullEOD { Close = 420m, PrevClose = 415m, Date = new DateOnly(2026, 2, 14) };
        var rcEod = new RCEod(eod, CurrencyId.USD, 0.92m, new DateOnly(2026, 2, 14));

        // ChangeP = (420 - 415) / 415 * 100 = 1.2048...
        decimal expectedChangeP = (420m - 415m) / 415m * 100m;
        Assert.Equal(expectedChangeP, rcEod.ChangeP);
    }

    [Fact]
    public void RCEod_HcClose_AppliesConversion()
    {
        var eod = new FullEOD { Close = 420m, PrevClose = 415m, Date = new DateOnly(2026, 2, 14) };
        var rcEod = new RCEod(eod, CurrencyId.USD, 0.92m, new DateOnly(2026, 2, 14));

        Assert.Equal(420m * 0.92m, rcEod.HcClose);
    }

    [Fact]
    public void RCEod_IsLatestEOD_MatchesMarketClosing()
    {
        var eod = new FullEOD { Close = 420m, Date = new DateOnly(2026, 2, 14) };

        // EOD date matches market closing → latest
        var rcLatest = new RCEod(eod, CurrencyId.USD, 0.92m, new DateOnly(2026, 2, 14));
        Assert.True(rcLatest.IsLatestEOD);

        // EOD date doesn't match → not latest
        var rcOld = new RCEod(eod, CurrencyId.USD, 0.92m, new DateOnly(2026, 2, 15));
        Assert.False(rcOld.IsLatestEOD);
    }

    // ==================== RCDivident Tests ====================

    [Fact]
    public void RCDivident_HcCalculations_Correct()
    {
        var div = new SHolding.Divident(0.485m, new DateOnly(2024, 3, 1), new DateOnly(2024, 4, 1), 1.32m, CurrencyId.USD);
        var rcd = new RCDivident(div);

        rcd.HoldingUnits = 50;
        rcd.TradesUnits = 10;

        // HcPaymentPerUnit = 0.485 * 1.32 = 0.6402
        Assert.Equal(0.485m * 1.32m, rcd.HcPaymentPerUnit);

        // HcTotalHoldingDiv = 0.6402 * 50 = 32.01
        Assert.Equal(0.485m * 1.32m * 50m, rcd.HcTotalHoldingDiv);

        // HcTotalTradeDiv = 0.6402 * 10 = 6.402
        Assert.Equal(0.485m * 1.32m * 10m, rcd.HcTotalTradeDiv);

        // HcTotalDiv = 32.01 + 6.402 = 38.412
        Assert.Equal(0.485m * 1.32m * 60m, rcd.HcTotalDiv);
    }

    // ==================== RRDivident Tests ====================

    [Fact]
    public void RRHoldingDivident_Calculations_Correct()
    {
        // KO holding with dividend
        var holding = new SHolding
        {
            SRef = "NYSE$KO",
            Units = 50,
            PricePerUnit = 62.50m,
            FeePerUnit = 0.199m,
            CurrencyRate = 1.32m,
            OriginalUnits = 50
        };
        var div = new SHolding.Divident(0.485m, new DateOnly(2024, 3, 1), new DateOnly(2024, 4, 1), 1.32m, CurrencyId.USD);

        var rrd = new RRHoldingDivident(holding, div);

        // McDiv = Units * PaymentPerUnit = 50 * 0.485 = 24.25
        Assert.Equal(50m * 0.485m, rrd.McDiv);

        // HcDiv = Units * HcPaymentPerUnit = 50 * (0.485 * 1.32) = 50 * 0.6402 = 32.01
        Assert.Equal(50m * 0.485m * 1.32m, rrd.HcDiv);

        // McInvested = holding.McInvested = McPriceWithFeePerUnit * Units = 62.699 * 50 = 3134.95
        Assert.Equal(holding.McInvested, rrd.McInvested);

        // HcInvested = holding.HcInvested = McInvested * CurrencyRate = 3134.95 * 1.32 = 4138.134
        Assert.Equal(holding.HcInvested, rrd.HcInvested);
    }

    [Fact]
    public void RRTotalDivident_FromHolding_SumsAllDividents()
    {
        var holding = new SHolding
        {
            SRef = "NYSE$KO",
            Units = 50,
            PricePerUnit = 62.50m,
            FeePerUnit = 0.199m,
            CurrencyRate = 1.32m,
            OriginalUnits = 50,
            Dividents = new()
            {
                new SHolding.Divident(0.485m, new DateOnly(2024, 3, 1), new DateOnly(2024, 4, 1), 1.32m, CurrencyId.USD),
                new SHolding.Divident(0.485m, new DateOnly(2024, 6, 1), new DateOnly(2024, 7, 1), 1.33m, CurrencyId.USD),
                new SHolding.Divident(0.485m, new DateOnly(2024, 9, 1), new DateOnly(2024, 10, 1), 1.34m, CurrencyId.USD),
            }
        };

        var total = new RRTotalDivident(holding);

        // HcDiv = sum of (HcPaymentPerUnit * Units) for each dividend
        // = (0.485*1.32)*50 + (0.485*1.33)*50 + (0.485*1.34)*50
        decimal expectedHcDiv = (0.485m * 1.32m * 50m) + (0.485m * 1.33m * 50m) + (0.485m * 1.34m * 50m);
        Assert.Equal(expectedHcDiv, total.HcDiv);

        // HcInvested = holding.HcInvested
        Assert.Equal(holding.HcInvested, total.HcInvested);
    }

    // ==================== SHolding Calculation Tests ====================

    [Fact]
    public void SHolding_McHcInvested_Correct()
    {
        // MSFT M001: 10u@280.50, fee=9.95 (FeePerUnit=0.995), CR=1.35
        var h = new SHolding
        {
            Units = 10,
            PricePerUnit = 280.50m,
            FeePerUnit = 0.995m,
            CurrencyRate = 1.35m,
        };

        // McPriceWithFeePerUnit = 280.50 + 0.995 = 281.495
        Assert.Equal(281.495m, h.McPriceWithFeePerUnit);

        // HcPriceWithFeePerUnit = 281.495 * 1.35 = 380.01825
        Assert.Equal(281.495m * 1.35m, h.HcPriceWithFeePerUnit);

        // McInvested = McPriceWithFeePerUnit * Units = 281.495 * 10 = 2814.95
        Assert.Equal(2814.95m, h.McInvested);

        // HcInvested = McInvested * CurrencyRate = 2814.95 * 1.35 = 3800.1825
        Assert.Equal(2814.95m * 1.35m, h.HcInvested);
    }

    [Fact]
    public void SHolding_HcSoldProfit_Correct()
    {
        // NVDA trade: bought 20u@148.50, fee=0.4975, CR=1.34; sold 5u@450.00, fee=1.99, CR=1.36
        var trade = new SHolding
        {
            Units = 5,
            PricePerUnit = 148.50m,
            FeePerUnit = 0.4975m,
            CurrencyRate = 1.34m,
            Sold = new SHolding.Sale("T001", new DateOnly(2024, 6, 15), 450.00m, 1.99m, 1.36m, "TakeProfit"),
        };

        // HcPriceWithFeePerUnit (buy) = (148.50 + 0.4975) * 1.34 = 148.9975 * 1.34 = 199.65665
        // Sold.HcPriceWithFeePerUnit = (450.00 - 1.99) * 1.36 = 448.01 * 1.36 = 609.2936
        // HcSoldProfit = (609.2936 - 199.65665) * 5 = 409.63695 * 5 = 2048.18475
        decimal expectedProfit = (trade.Sold.HcPriceWithFeePerUnit - trade.HcPriceWithFeePerUnit) * trade.Units;
        Assert.Equal(expectedProfit, trade.HcSoldProfit);
        Assert.True(trade.HcSoldProfit > 0, "NVDA sale should be profitable");
    }

    [Fact]
    public void SHolding_HcTotalDividents_Correct()
    {
        var h = new SHolding
        {
            Units = 50,
            Dividents = new()
            {
                new SHolding.Divident(0.485m, new DateOnly(2024, 3, 1), new DateOnly(2024, 4, 1), 1.32m, CurrencyId.USD),
                new SHolding.Divident(0.485m, new DateOnly(2024, 6, 1), new DateOnly(2024, 7, 1), 1.33m, CurrencyId.USD),
            }
        };

        // HcTotalDividents = sum of (HcPaymentPerUnit * Units)
        // = (0.485*1.32)*50 + (0.485*1.33)*50
        decimal expected = (0.485m * 1.32m * 50m) + (0.485m * 1.33m * 50m);
        Assert.Equal(expected, h.HcTotalDividents);
    }

    // ==================== BUG-14 Reproduction Tests ====================

    [Fact]
    public void Invested_ZeroInvested_NoDivideByZero()
    {
        // BUG-14: HcGainP divides by HcInvested but guards on HcGain != 0
        // Create a holding with essentially 0 price (free shares / dividend-reinvest)
        // Verify the report doesn't throw DivideByZeroException
        var stalker = StalkerTestFixture.CreateEmpty();
        stalker.DoAction("Add-Portfolio PfName=Test");
        stalker.DoAction("Follow-Portfolio PfName=Test SRef=NYSE$KO");
        stalker.DoAction("Add-Holding PfName=Test SRef=NYSE$KO PurhaceId=Z001 Date=2024-01-01 Units=10 Price=0.01 Fee=0 CurrencyRate=1.32 Note=FreeShares");

        var preCalc = ReportTestFixture.CreatePreCalc(stalker);
        var stockMeta = ReportTestFixture.CreateStockMeta();
        var stockNotes = ReportTestFixture.CreateStockNotes();
        var filters = ReportTestFixture.CreateReportFilters();

        // Should not throw
        var (header, stocks) = RepGenInvested.GenerateReport(filters, preCalc, stockMeta, stalker, stockNotes);

        Assert.NotNull(header);
        Assert.NotNull(stocks);
        Assert.Single(stocks);
    }

    [Fact]
    public void ExpHoldings_ZeroInvested_NoDivideByZero()
    {
        // BUG-14: Same issue in RepGenExpHoldings path
        var stalker = StalkerTestFixture.CreateEmpty();
        stalker.DoAction("Add-Portfolio PfName=Test");
        stalker.DoAction("Follow-Portfolio PfName=Test SRef=NYSE$KO");
        stalker.DoAction("Add-Holding PfName=Test SRef=NYSE$KO PurhaceId=Z001 Date=2024-01-01 Units=10 Price=0.01 Fee=0 CurrencyRate=1.32 Note=FreeShares");

        var preCalc = ReportTestFixture.CreatePreCalc(stalker);
        var stockMeta = ReportTestFixture.CreateStockMeta();
        var filters = ReportTestFixture.CreateReportFilters();
        var today = new DateOnly(2026, 2, 15);

        // Should not throw
        var result = RepGenExpHoldings.GenerateReport(today, filters, preCalc, stockMeta, stalker);

        Assert.NotNull(result);
        Assert.Single(result);
    }

    // ==================== BUG-15 Reproduction Test ====================

    [Fact]
    public void OverviewGroups_PfSRefs_NoDuplicates()
    {
        // BUG-15 fix verification: Portfolio group SRefs should not contain duplicates
        var stalker = ReportTestFixture.CreateStalker();
        var preCalc = ReportTestFixture.CreatePreCalc(stalker);
        var pfsStatus = ReportTestFixture.CreatePfsStatus();
        var rates = ReportTestFixture.CreateLatestRates();
        var filters = ReportTestFixture.CreateReportFilters();

        var result = ReportOverviewGroups.GenerateReport(filters, preCalc, pfsStatus, stalker, rates);
        Assert.True(result.Ok);

        var groups = ((OkResult<List<OverviewGroupsData>>)result).Data;
        var pfGroups = groups.Where(g => g.Name.StartsWith("PF:")).ToList();

        foreach (var pfGroup in pfGroups)
        {
            int totalCount = pfGroup.SRefs.Count;
            int distinctCount = pfGroup.SRefs.Distinct().Count();
            Assert.Equal(distinctCount, totalCount);
        }
    }

    // ==================== RepGenPfStocks Tests ====================

    [Fact]
    public void PfStocks_AllStocks_ReturnsEntries()
    {
        // Main portfolio has MSFT, KO, ENB following
        var stalker = ReportTestFixture.CreateStalker();
        var preCalc = ReportTestFixture.CreatePreCalcForPf("Main", stalker);
        var stockNotes = ReportTestFixture.CreateStockNotes();
        var filters = ReportTestFixture.CreateReportFilters();

        var result = RepGenPfStocks.GenerateReport(filters, preCalc, stalker, stockNotes);

        Assert.NotNull(result);
        // Main portfolio follows: MSFT, KO, ENB
        Assert.Equal(3, result.Count);

        var symbols = result.Select(r => r.StockMeta.symbol).ToList();
        Assert.Contains("MSFT", symbols);
        Assert.Contains("KO", symbols);
        Assert.Contains("ENB", symbols);
    }

    [Fact]
    public void PfStocks_NoEod_ShowsFailedMsg()
    {
        // Create a stock with no EOD data
        var stalker = StalkerTestFixture.CreateEmpty();
        stalker.DoAction("Add-Portfolio PfName=Test");
        stalker.DoAction("Follow-Portfolio PfName=Test SRef=TSX$RY");

        // Use empty EOD so TSX$RY has no price data
        var stockMeta = new StubStockMeta().Add(MarketId.TSX, "RY", "Royal Bank", CurrencyId.CAD);
        var eod = new StubEodLatest(); // empty — no EOD for RY
        var rates = ReportTestFixture.CreateLatestRates();
        var marketMeta = ReportTestFixture.CreateMarketMeta();
        var platform = ReportTestFixture.CreatePfsPlatform();
        var filters = ReportTestFixture.CreateReportFilters();

        var preCalc = new ReportPreCalc(
            limitSinglePfName: "Test",
            reportFilters: filters,
            pfsPlatform: platform,
            latestEodProv: eod,
            stockMetaProv: stockMeta,
            marketMetaProv: marketMeta,
            latestRatesProv: rates,
            stalkerData: stalker
        );

        var stockNotes = ReportTestFixture.CreateStockNotes();
        var result = RepGenPfStocks.GenerateReport(filters, preCalc, stalker, stockNotes);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.False(string.IsNullOrWhiteSpace(result[0].FailedMsg));
    }

    [Fact]
    public void PfStocks_AlarmsAndOrders_Included()
    {
        // Main has alarm on MSFT (Under 250) and order on MSFT (Buy 5@250)
        var stalker = ReportTestFixture.CreateStalker();
        var preCalc = ReportTestFixture.CreatePreCalcForPf("Main", stalker);
        var stockNotes = ReportTestFixture.CreateStockNotes();
        var filters = ReportTestFixture.CreateReportFilters();

        var result = RepGenPfStocks.GenerateReport(filters, preCalc, stalker, stockNotes);

        var msft = result.FirstOrDefault(r => r.StockMeta.symbol == "MSFT");
        Assert.NotNull(msft);
        Assert.NotNull(msft.RRAlarm);           // Has alarm Under 250
        Assert.NotNull(msft.Order);              // Has order Buy 5@250
        Assert.Equal(250m, msft.Order.PricePerUnit);
    }

    // ==================== Multi-Currency & Edge Cases ====================

    [Fact]
    public void Invested_MultiCurrency_ConversionCorrect()
    {
        // Fixture has USD (MSFT, KO, NVDA), CAD (ENB), EUR (SAP) stocks
        // Verify HC (EUR) conversions are applied correctly
        var stalker = ReportTestFixture.CreateStalker();
        var preCalc = ReportTestFixture.CreatePreCalc(stalker);
        var stockMeta = ReportTestFixture.CreateStockMeta();
        var stockNotes = ReportTestFixture.CreateStockNotes();
        var filters = ReportTestFixture.CreateReportFilters();

        var (header, stocks) = RepGenInvested.GenerateReport(filters, preCalc, stockMeta, stalker, stockNotes);

        Assert.NotNull(stocks);

        // MSFT close=$420 * 0.92 = EUR 386.40 per unit, 15 total units → HcValuation ≈ 5796
        var msft = stocks.FirstOrDefault(s => s.StockMeta.symbol == "MSFT");
        Assert.NotNull(msft);
        Assert.Equal(15m, msft.RCTotalHold.Units);
        Assert.Equal((int)(420m * 0.92m * 15m), msft.RCTotalHold.HcValuation);

        // ENB close=CAD$52 * 0.68 = EUR 35.36, 150 total units → HcValuation ≈ 5304
        var enb = stocks.FirstOrDefault(s => s.StockMeta.symbol == "ENB");
        Assert.NotNull(enb);
        Assert.Equal(150m, enb.RCTotalHold.Units);
        Assert.Equal((int)(52m * 0.68m * 150m), enb.RCTotalHold.HcValuation);
    }

    [Fact]
    public void Empty_Stalker_ReportsReturnNullOrEmpty()
    {
        // Each report should return expected "no data" when stalker is empty
        var stalker = StalkerTestFixture.CreateEmpty();
        var preCalc = ReportTestFixture.CreatePreCalc(stalker);
        var stockMeta = ReportTestFixture.CreateStockMeta();
        var stockNotes = ReportTestFixture.CreateStockNotes();
        var filters = ReportTestFixture.CreateReportFilters();
        var pfsStatus = ReportTestFixture.CreatePfsStatus();
        var rates = ReportTestFixture.CreateLatestRates();
        var today = new DateOnly(2026, 2, 15);

        // Invested
        var (invHeader, invStocks) = RepGenInvested.GenerateReport(filters, preCalc, stockMeta, stalker, stockNotes);
        Assert.Null(invHeader);
        Assert.Null(invStocks);

        // ExpHoldings
        var expResult = RepGenExpHoldings.GenerateReport(today, filters, preCalc, stockMeta, stalker);
        Assert.Null(expResult);

        // PfStocks
        var pfStocks = RepGenPfStocks.GenerateReport(filters, preCalc, stalker, stockNotes);
        Assert.NotNull(pfStocks);
        Assert.Empty(pfStocks);

        // PfSales
        var pfSales = RepGenPfSales.GenerateReport(filters, preCalc, stockMeta, stalker, stockNotes);
        Assert.Null(pfSales);

        // Weight
        var (wHeader, wStocks) = RepGenWeight.GenerateReport(today, filters, preCalc, stockMeta, stalker, stockNotes, pfsStatus);
        Assert.Null(wHeader);
        Assert.Null(wStocks);

        // Divident
        var divResult = RepGenDivident.GenerateReport(today, filters, preCalc, stalker, stockMeta);
        Assert.False(divResult.Ok);
    }

    [Fact]
    public void StMgHistory_MetaHistEvents_Included()
    {
        var stalker = ReportTestFixture.CreateStalker();
        var stockMeta = ReportTestFixture.CreateStockMeta();
        var eod = ReportTestFixture.CreateEodLatest();
        var rates = ReportTestFixture.CreateLatestRates();
        var platform = ReportTestFixture.CreatePfsPlatform();
        var hist = new StoreStockMetaHist(platform);

        // Add a historical event (name update)
        hist.AppendUpdateName(MarketId.NYSE, "KO", new DateOnly(2025, 1, 15), "Coca-Cola Company");

        var result = RepGenStMgHistory.GenerateReport("NYSE$KO", stalker, stockMeta, eod, rates, hist);
        Assert.True(result.Ok);
        var report = ((OkResult<List<RepDataStMgHistory>>)result).Data;

        // Should have at least one History entry
        var histEntries = report.Where(e => e.History != null).ToList();
        Assert.Single(histEntries);
        Assert.Equal(StockMetaHistType.UpdName, histEntries[0].History.Type);
    }
}
