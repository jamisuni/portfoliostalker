using Pfs.Types;
using Xunit;

namespace PfsReports.Tests.Tests;

public class RCGrowthTests
{
    [Fact]
    public void Growth_BasicProfit_CalculationsCorrect()
    {
        // 10 units bought at $100 each (MC), $135 each (HC), now trading at $150 (MC)
        // HC close = $150 * 0.92 conversion = $138
        var g = new RCGrowth(
            Units: 10m,
            McInvested: 1000m,      // 10 * $100
            HcInvested: 1350m,      // 10 * $135
            McClosePrice: 150m,
            HcClosePrice: 138m      // 150 * 0.92
        );

        // McAvrgPrice = 1000 / 10 = 100
        Assert.Equal(100m, g.McAvrgPrice);
        // McGrowthP = (150 - 100) / 100 * 100 = 50%
        Assert.Equal(50, g.McGrowthP);
        // McGrowthAmount = 150 * 10 - 1000 = 500
        Assert.Equal(500, g.McGrowthAmount);

        // HcAvrgPrice = 1350 / 10 = 135
        Assert.Equal(135m, g.HcAvrgPrice);
        // HcGrowthP = (138 - 135) / 135 * 100 = 2.22... → truncated to 2
        Assert.Equal(2, g.HcGrowthP);
        // HcValuation = 138 * 10 = 1380
        Assert.Equal(1380, g.HcValuation);
        // HcGrowthAmount = 138 * 10 - 1350 = 30
        Assert.Equal(30, g.HcGrowthAmount);
    }

    [Fact]
    public void Growth_WithLoss_NegativeValues()
    {
        // 10 units bought at $100, now at $80
        var g = new RCGrowth(10m, 1000m, 1000m, 80m, 80m);

        Assert.Equal(100m, g.McAvrgPrice);
        // (80 - 100) / 100 * 100 = -20%
        Assert.Equal(-20, g.McGrowthP);
        // 80 * 10 - 1000 = -200
        Assert.Equal(-200, g.McGrowthAmount);

        Assert.Equal(800, g.HcValuation);
        Assert.Equal(-200, g.HcGrowthAmount);
    }

    [Fact]
    public void Growth_FromHolding_UsesConversionRate()
    {
        // Simulates RCGrowth(SHolding, mcClosePrice, latestConversionRate) constructor
        // Holding: 50 units, PricePerUnit=62.50, FeePerUnit=0.199 (9.95/50), CurrencyRate=1.32
        var holding = new SHolding
        {
            Units = 50,
            PricePerUnit = 62.50m,
            FeePerUnit = 0.199m,    // 9.95 / 50
            CurrencyRate = 1.32m
        };

        decimal mcClose = 62m;
        decimal conversionRate = 0.92m;

        var g = new RCGrowth(holding, mcClose, conversionRate);

        // McInvested = McPriceWithFeePerUnit * Units = (62.50 + 0.199) * 50 = 62.699 * 50 = 3134.95
        Assert.Equal(holding.McInvested, g.McInvested);
        // HcInvested = McInvested * CurrencyRate = 3134.95 * 1.32 = 4138.134
        Assert.Equal(holding.HcInvested, g.HcInvested);
        // Units
        Assert.Equal(50m, g.Units);
        // McClosePrice = 62
        Assert.Equal(62m, g.McClosePrice);
        // HcClosePrice = 62 * 0.92 = 57.04
        Assert.Equal(62m * 0.92m, g.HcClosePrice);
        // McAvrgPrice = 3134.95 / 50 = 62.699
        Assert.Equal(holding.McPriceWithFeePerUnit, g.McAvrgPrice);
    }

    [Fact]
    public void Growth_TinyAvrgPrice_GrowthPIsZero()
    {
        // If average price is <= 0.01, growth% should be 0
        var g = new RCGrowth(100m, 0.5m, 0.5m, 10m, 10m);

        // McAvrgPrice = 0.5 / 100 = 0.005 which is < 0.01
        Assert.Equal(0, g.McGrowthP);
        Assert.Equal(0, g.HcGrowthP);

        // But amount is still calculated
        // McGrowthAmount = 10 * 100 - 0.5 = 999 (truncated to int)
        Assert.Equal(999, g.McGrowthAmount);
    }

    [Fact]
    public void Growth_FixtureKO_MatchesExpected()
    {
        // KO: 50u @ $62.50 + $9.95 fee = FeePerUnit=0.199, CurrencyRate=1.32
        // Current close: $62, ConversionRate: 0.92
        var holding = new SHolding
        {
            Units = 50,
            PricePerUnit = 62.50m,
            FeePerUnit = 0.199m,
            CurrencyRate = 1.32m
        };

        var g = new RCGrowth(holding, 62m, 0.92m);

        // McPriceWithFeePerUnit = 62.699
        // McInvested = 62.699 * 50 = 3134.95
        // McAvrgPrice = 62.699
        // McGrowthP = (62 - 62.699) / 62.699 * 100 = -1.11... → -1
        Assert.Equal(-1, g.McGrowthP);

        // HcClosePrice = 62 * 0.92 = 57.04
        // HcInvested = 3134.95 * 1.32 = 4138.134
        // HcAvrgPrice = 4138.134 / 50 = 82.76268
        // HcGrowthP = (57.04 - 82.76268) / 82.76268 * 100 = -31.09... → -31
        Assert.Equal(-31, g.HcGrowthP);

        // HcValuation = 57.04 * 50 = 2852
        Assert.Equal(2852, g.HcValuation);
    }
}
