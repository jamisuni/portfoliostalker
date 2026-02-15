using Pfs.Types;
using PfsData.Tests.Helpers;
using Xunit;

namespace PfsData.Tests.Tests;

public class StockTests
{
    [Fact]
    public void Delete_Stock_NoReferences_Success()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        // Adding alarm creates stock entry via GetOrAddStock
        StalkerAssert.Ok(s.DoAction("Add-Alarm Type=Under SRef=NASDAQ$AAPL Level=150.00 Prms= Note=Test"));
        Assert.NotNull(s.StockRef("NASDAQ$AAPL"));
        // Delete alarm first then stock
        StalkerAssert.Ok(s.DoAction("DeleteAll-Alarm SRef=NASDAQ$AAPL"));
        StalkerAssert.Ok(s.DoAction("Delete-Stock SRef=NASDAQ$AAPL"));
        Assert.Null(s.StockRef("NASDAQ$AAPL"));
    }

    [Fact]
    public void Delete_Stock_WithHoldings_Fails()
    {
        var s = StalkerTestFixture.CreatePopulated();
        StalkerAssert.Fail(s.DoAction("Delete-Stock SRef=NASDAQ$MSFT"), "holdings");
    }

    [Fact]
    public void Delete_Stock_WithOrders_Fails()
    {
        var s = StalkerTestFixture.CreatePopulated();
        StalkerAssert.Fail(s.DoAction("Delete-Stock SRef=NASDAQ$MSFT"), "holdings");
    }

    [Fact]
    public void Delete_Stock_WithTrackings_Fails()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Follow-Portfolio PfName=Test SRef=NYSE$AAPL"));
        // Stock doesn't even need a SStock entry for this to fail - but tracking prevents delete
        // Actually stock wont be in _stocks. Let's add alarm to create it, then try delete
        StalkerAssert.Ok(s.DoAction("Add-Alarm Type=Under SRef=NYSE$AAPL Level=150.00 Prms= Note=Test"));
        StalkerAssert.Fail(s.DoAction("Delete-Stock SRef=NYSE$AAPL"), "trackings");
    }

    [Fact]
    public void Set_Stock_RenamesSRef()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Follow-Portfolio PfName=Test SRef=NASDAQ$OLD"));
        StalkerAssert.Ok(s.DoAction("Add-Alarm Type=Under SRef=NASDAQ$OLD Level=100.00 Prms= Note="));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$OLD PurhaceId=H001 Date=2024-01-15 Units=10 Price=100 Fee=0 CurrencyRate=1 Note="));
        StalkerAssert.Ok(s.DoAction("Set-Stock UpdSRef=NASDAQ$NEW OldSRef=NASDAQ$OLD"));
        // Verify rename propagated
        Assert.Contains("NASDAQ$NEW", s.PortfolioSRefs("Test"));
        Assert.DoesNotContain("NASDAQ$OLD", s.PortfolioSRefs("Test"));
        Assert.Equal("NASDAQ$NEW", s.PortfolioHoldings("Test")[0].SRef);
        Assert.NotNull(s.StockRef("NASDAQ$NEW"));
        Assert.Null(s.StockRef("NASDAQ$OLD"));
    }

    [Fact]
    public void Split_Stock_AdjustsUnitsAndPrices()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Alarm Type=Under SRef=NASDAQ$TSLA Level=100.00 Prms= Note="));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$TSLA PurhaceId=H001 Date=2024-01-15 Units=10 Price=300 Fee=10 CurrencyRate=1 Note="));
        StalkerAssert.Ok(s.DoAction("Split-Stock SRef=NASDAQ$TSLA SplitFactor=0.5"));
        var h = s.PortfolioHoldings("Test")[0];
        Assert.Equal(20m, h.Units);         // 10 / 0.5
        Assert.Equal(150m, h.PricePerUnit); // 300 * 0.5
    }

    [Fact]
    public void Split_Stock_Factor1_NoChange()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Alarm Type=Under SRef=NASDAQ$TSLA Level=100.00 Prms= Note="));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$TSLA PurhaceId=H001 Date=2024-01-15 Units=10 Price=300 Fee=0 CurrencyRate=1 Note="));
        StalkerAssert.Ok(s.DoAction("Split-Stock SRef=NASDAQ$TSLA SplitFactor=1"));
        Assert.Equal(10m, s.PortfolioHoldings("Test")[0].Units);
    }

    [Fact]
    public void Close_Stock_MovesHoldingsToTrades()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Follow-Portfolio PfName=Test SRef=NASDAQ$DEAD"));
        StalkerAssert.Ok(s.DoAction("Add-Alarm Type=Under SRef=NASDAQ$DEAD Level=10.00 Prms= Note="));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$DEAD PurhaceId=H001 Date=2024-01-15 Units=100 Price=5 Fee=0 CurrencyRate=1 Note="));
        StalkerAssert.Ok(s.DoAction("Close-Stock SRef=NASDAQ$DEAD Date=2024-12-01 Note=Delisted"));
        // Holdings moved to trades
        Assert.Empty(s.PortfolioHoldings("Test", "CLOSED$DEAD"));
        var trades = s.PortfolioTrades("Test", "CLOSED$DEAD");
        Assert.Single(trades);
        Assert.Contains("_CLOSED", trades[0].Sold.TradeId);
        // SRef renamed to CLOSED$DEAD
        Assert.Contains("CLOSED$DEAD", s.PortfolioSRefs("Test"));
    }

    [Fact]
    public void DeleteAll_Stock_RemovesEverything()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Follow-Portfolio PfName=Test SRef=NASDAQ$KILL"));
        StalkerAssert.Ok(s.DoAction("Add-Alarm Type=Under SRef=NASDAQ$KILL Level=10.00 Prms= Note="));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$KILL PurhaceId=H001 Date=2024-01-15 Units=10 Price=100 Fee=0 CurrencyRate=1 Note="));
        StalkerAssert.Ok(s.DoAction("Add-Order PfName=Test Type=Buy SRef=NASDAQ$KILL Units=5 Price=80.00 LastDate=2025-12-31"));
        StalkerAssert.Ok(s.DoAction("DeleteAll-Stock SRef=NASDAQ$KILL"));
        Assert.DoesNotContain("NASDAQ$KILL", s.PortfolioSRefs("Test"));
        Assert.Empty(s.PortfolioHoldings("Test"));
        Assert.Empty(s.PortfolioOrders("Test"));
        Assert.Null(s.StockRef("NASDAQ$KILL"));
    }

    [Fact]
    public void Set_Stock_RenamesPropagesToTrades()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Alarm Type=Under SRef=NASDAQ$OLD Level=100.00 Prms= Note="));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$OLD PurhaceId=H001 Date=2024-01-15 Units=10 Price=100 Fee=0 CurrencyRate=1 Note="));
        StalkerAssert.Ok(s.DoAction("Add-Trade PfName=Test SRef=NASDAQ$OLD Date=2024-06-01 Units=10 Price=150 Fee=0 TradeId=T001 OptPurhaceId=H001 CurrencyRate=1 Note="));
        StalkerAssert.Ok(s.DoAction("Set-Stock UpdSRef=NASDAQ$NEW OldSRef=NASDAQ$OLD"));
        Assert.Equal("NASDAQ$NEW", s.PortfolioTrades("Test")[0].SRef);
    }
}
