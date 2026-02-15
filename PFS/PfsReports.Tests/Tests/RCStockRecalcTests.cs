using Pfs.Types;
using PfsReports.Tests.Helpers;
using Xunit;

namespace PfsReports.Tests.Tests;

public class RCStockRecalcTests
{
    private static RCStock CreateStockWithEod(string sRef, decimal close, decimal prevClose, CurrencyId currency, decimal convRate)
    {
        var (marketId, symbol) = StockMeta.ParseSRef(sRef);
        var sm = new StockMeta(marketId, symbol, "Test", currency);
        var stock = new SStock(sRef);
        var rcs = new RCStock(stock, sm);
        rcs.RCEod = new RCEod(
            new FullEOD { Close = close, PrevClose = prevClose, Date = new DateOnly(2026, 2, 14) },
            currency, convRate, new DateOnly(2026, 2, 14));
        return rcs;
    }

    [Fact]
    public void RecalcTotals_SingleHolding_CorrectGrowth()
    {
        var rcs = CreateStockWithEod("NYSE$KO", 62m, 61.5m, CurrencyId.USD, 0.92m);

        // KO K001: 50u @ $62.50 + $9.95 fee (FeePerUnit = 0.199), CurrencyRate = 1.32
        var holding = new SHolding
        {
            SRef = "NYSE$KO",
            Units = 50,
            PricePerUnit = 62.50m,
            FeePerUnit = 0.199m,
            CurrencyRate = 1.32m,
            OriginalUnits = 50
        };
        rcs.Holdings.Add(new RCHolding(holding, "Main"));
        rcs.RecalculateTotals();

        Assert.NotNull(rcs.RCTotalHold);
        Assert.Equal(50m, rcs.RCTotalHold.Units);

        // McInvested from RecalcTotals = Units * McPriceWithFeePerUnit = 50 * (62.50 + 0.199) = 50 * 62.699 = 3134.95
        Assert.Equal(50m * 62.699m, rcs.RCTotalHold.McInvested);

        // HcInvested from RecalcTotals = Units * HcPriceWithFeePerUnit = 50 * (62.699 * 1.32) = 50 * 82.76268 = 4138.134
        Assert.Equal(50m * (62.699m * 1.32m), rcs.RCTotalHold.HcInvested);

        // Close = $62, HcClose = 62 * 0.92 = 57.04
        Assert.Equal(62m, rcs.RCTotalHold.McClosePrice);
        Assert.Equal(62m * 0.92m, rcs.RCTotalHold.HcClosePrice);
    }

    [Fact]
    public void RecalcTotals_TwoHoldings_AggregatesCorrectly()
    {
        var rcs = CreateStockWithEod("NASDAQ$MSFT", 420m, 415m, CurrencyId.USD, 0.92m);

        // MSFT M001: 10u@280.50, fee=0.995 (9.95/10), CR=1.35
        var h1 = new SHolding { SRef = "NASDAQ$MSFT", Units = 10, PricePerUnit = 280.50m,
            FeePerUnit = 0.995m, CurrencyRate = 1.35m, OriginalUnits = 10 };
        // MSFT M002: 5u@330.00, fee=0.99 (4.95/5), CR=1.36
        var h2 = new SHolding { SRef = "NASDAQ$MSFT", Units = 5, PricePerUnit = 330.00m,
            FeePerUnit = 0.99m, CurrencyRate = 1.36m, OriginalUnits = 5 };

        rcs.Holdings.Add(new RCHolding(h1, "Main"));
        rcs.Holdings.Add(new RCHolding(h2, "Main"));
        rcs.RecalculateTotals();

        // Total units = 10 + 5 = 15
        Assert.Equal(15m, rcs.RCTotalHold.Units);

        // McInvested = 10*(280.50+0.995) + 5*(330.00+0.99) = 10*281.495 + 5*330.99 = 2814.95 + 1654.95 = 4469.90
        decimal expectedMcInvested = 10m * 281.495m + 5m * 330.99m;
        Assert.Equal(expectedMcInvested, rcs.RCTotalHold.McInvested);

        // HcInvested = 10*(281.495*1.35) + 5*(330.99*1.36) = 10*380.01825 + 5*450.1464 = 3800.1825 + 2250.732 = 6050.9145
        decimal expectedHcInvested = 10m * (281.495m * 1.35m) + 5m * (330.99m * 1.36m);
        Assert.Equal(expectedHcInvested, rcs.RCTotalHold.HcInvested);

        // McAvrgPrice = 4469.90 / 15 = 297.993...
        Assert.Equal(expectedMcInvested / 15m, rcs.RCTotalHold.McAvrgPrice);
    }

    [Fact]
    public void RecalcTotals_NoEod_SetsNulls()
    {
        var (marketId, symbol) = StockMeta.ParseSRef("NYSE$KO");
        var sm = new StockMeta(marketId, symbol, "KO", CurrencyId.USD);
        var stock = new SStock("NYSE$KO");
        var rcs = new RCStock(stock, sm);
        // No RCEod set

        var holding = new SHolding { SRef = "NYSE$KO", Units = 50, PricePerUnit = 62.50m,
            FeePerUnit = 0.199m, CurrencyRate = 1.32m, OriginalUnits = 50 };
        rcs.Holdings.Add(new RCHolding(holding, "Main"));
        rcs.RecalculateTotals();

        Assert.Null(rcs.RCTotalHold);
        Assert.Null(rcs.RCHoldingsTotalDivident);
    }

    [Fact]
    public void RecalcTotals_NoHoldings_SetsNulls()
    {
        var rcs = CreateStockWithEod("NYSE$KO", 62m, 61.5m, CurrencyId.USD, 0.92m);
        // No holdings added
        rcs.RecalculateTotals();

        Assert.Null(rcs.RCTotalHold);
        Assert.Null(rcs.RCHoldingsTotalDivident);
    }

    [Fact]
    public void RecalcTotals_WithDividends_TotalDivCorrect()
    {
        var rcs = CreateStockWithEod("NYSE$KO", 62m, 61.5m, CurrencyId.USD, 0.92m);

        var holding = new SHolding { SRef = "NYSE$KO", Units = 50, PricePerUnit = 62.50m,
            FeePerUnit = 0.199m, CurrencyRate = 1.32m, OriginalUnits = 50 };
        rcs.Holdings.Add(new RCHolding(holding, "Main"));

        // Add 3 quarterly dividends (matching fixture: 0.485/unit, different CurrencyRates)
        rcs.AddHoldingDivident(50, new SHolding.Divident(0.485m, new DateOnly(2024, 3, 1), new DateOnly(2024, 4, 1), 1.32m, CurrencyId.USD));
        rcs.AddHoldingDivident(50, new SHolding.Divident(0.485m, new DateOnly(2024, 6, 1), new DateOnly(2024, 7, 1), 1.33m, CurrencyId.USD));
        rcs.AddHoldingDivident(50, new SHolding.Divident(0.485m, new DateOnly(2024, 9, 1), new DateOnly(2024, 10, 1), 1.34m, CurrencyId.USD));

        rcs.RecalculateTotals();

        Assert.NotNull(rcs.RCHoldingsTotalDivident);

        // HcHoldingDiv = sum of (HoldingUnits * PaymentPerUnit * CurrencyRate) per payment date
        // = 50 * 0.485 * 1.32 + 50 * 0.485 * 1.33 + 50 * 0.485 * 1.34
        decimal expectedHcDiv = 50m * 0.485m * 1.32m + 50m * 0.485m * 1.33m + 50m * 0.485m * 1.34m;
        Assert.Equal(expectedHcDiv, rcs.RCHoldingsTotalDivident.HcDiv);
    }

    [Fact]
    public void RecalcTotals_QuarterlyDividents_DivPaidTimesIs4()
    {
        var rcs = CreateStockWithEod("NYSE$KO", 62m, 61.5m, CurrencyId.USD, 0.92m);

        var holding = new SHolding { SRef = "NYSE$KO", Units = 50, PricePerUnit = 62.50m,
            FeePerUnit = 0.199m, CurrencyRate = 1.32m, OriginalUnits = 50 };
        rcs.Holdings.Add(new RCHolding(holding, "Main"));

        // 3 quarterly dividends — 3 months apart
        rcs.AddHoldingDivident(50, new SHolding.Divident(0.485m, new DateOnly(2024, 3, 1), new DateOnly(2024, 4, 1), 1.32m, CurrencyId.USD));
        rcs.AddHoldingDivident(50, new SHolding.Divident(0.485m, new DateOnly(2024, 6, 1), new DateOnly(2024, 7, 1), 1.33m, CurrencyId.USD));
        rcs.AddHoldingDivident(50, new SHolding.Divident(0.485m, new DateOnly(2024, 9, 1), new DateOnly(2024, 10, 1), 1.34m, CurrencyId.USD));

        rcs.RecalculateTotals();

        // Latest 2 dividents: Oct (month 10) and Jul (month 7), diff = 3 months → quarterly → 4
        Assert.Equal(4, rcs.DivPaidTimesPerYear);
    }

    [Fact]
    public void RecalcTotals_MonthlyDividents_DivPaidTimesIs12()
    {
        var rcs = CreateStockWithEod("TSX$ENB", 52m, 51.5m, CurrencyId.CAD, 0.68m);

        var holding = new SHolding { SRef = "TSX$ENB", Units = 100, PricePerUnit = 54.20m,
            FeePerUnit = 0, CurrencyRate = 1, OriginalUnits = 100 };
        rcs.Holdings.Add(new RCHolding(holding, "Dividend"));

        // Monthly dividends — 1 month apart
        rcs.AddHoldingDivident(100, new SHolding.Divident(0.915m, new DateOnly(2024, 2, 15), new DateOnly(2024, 3, 15), 1m, CurrencyId.CAD));
        rcs.AddHoldingDivident(100, new SHolding.Divident(0.915m, new DateOnly(2024, 3, 15), new DateOnly(2024, 4, 15), 1m, CurrencyId.CAD));

        rcs.RecalculateTotals();

        // Diff = 1 month → monthly → 12
        Assert.Equal(12, rcs.DivPaidTimesPerYear);
    }

    [Fact]
    public void RecalcTotals_YearlyDividents_DivPaidTimesIs1()
    {
        var rcs = CreateStockWithEod("TSX$ENB", 52m, 51.5m, CurrencyId.CAD, 0.68m);

        var holding = new SHolding { SRef = "TSX$ENB", Units = 100, PricePerUnit = 54.20m,
            FeePerUnit = 0, CurrencyRate = 1, OriginalUnits = 100 };
        rcs.Holdings.Add(new RCHolding(holding, "Dividend"));

        // Yearly dividends — 12 months apart
        rcs.AddHoldingDivident(100, new SHolding.Divident(3.66m, new DateOnly(2023, 4, 15), new DateOnly(2023, 5, 15), 1m, CurrencyId.CAD));
        rcs.AddHoldingDivident(100, new SHolding.Divident(3.66m, new DateOnly(2024, 4, 15), new DateOnly(2024, 5, 15), 1m, CurrencyId.CAD));

        rcs.RecalculateTotals();

        // Diff = 12 months → yearly → 1
        Assert.Equal(1, rcs.DivPaidTimesPerYear);
    }

    [Fact]
    public void RecalcTotals_YearlyDivP_CalculationCorrect()
    {
        var rcs = CreateStockWithEod("NYSE$KO", 62m, 61.5m, CurrencyId.USD, 0.92m);

        var holding = new SHolding { SRef = "NYSE$KO", Units = 50, PricePerUnit = 62.50m,
            FeePerUnit = 0.199m, CurrencyRate = 1.32m, OriginalUnits = 50 };
        rcs.Holdings.Add(new RCHolding(holding, "Main"));

        // Quarterly dividends
        rcs.AddHoldingDivident(50, new SHolding.Divident(0.485m, new DateOnly(2024, 6, 1), new DateOnly(2024, 7, 1), 1.33m, CurrencyId.USD));
        rcs.AddHoldingDivident(50, new SHolding.Divident(0.485m, new DateOnly(2024, 9, 1), new DateOnly(2024, 10, 1), 1.34m, CurrencyId.USD));

        rcs.RecalculateTotals();

        // DivPaidTimesPerYear = 4 (quarterly)
        Assert.Equal(4, rcs.DivPaidTimesPerYear);

        // Latest dividend: HcPaymentPerUnit = 0.485 * 1.34 = 0.6499
        // HcAvrgPrice = HcInvested / Units
        decimal hcInvested = 50m * (62.699m * 1.32m); // = 4138.134
        decimal hcAvrgPrice = hcInvested / 50m;       // = 82.76268

        // YearlyDivP = HcPaymentPerUnit * DivPaidTimesPerYear * 100 / HcAvrgPrice
        //            = 0.6499 * 4 * 100 / 82.76268 = 259.96 / 82.76268 = 3.14...
        decimal latestHcPaymentPerUnit = 0.485m * 1.34m;
        decimal expectedDivP = latestHcPaymentPerUnit * 4m * 100m / hcAvrgPrice;
        Assert.Equal(expectedDivP, rcs.YearlyDivPForHcHolding);
    }
}
