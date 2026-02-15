using Pfs.Types;
using PfsData.Tests.Helpers;
using Xunit;

namespace PfsData.Tests.Tests;

public class TradeTests
{
    [Fact]
    public void Trade_FullSale_MovesHoldingToTrades()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$MSFT PurhaceId=H001 Date=2024-01-15 Units=10 Price=100 Fee=0 CurrencyRate=1 Note="));
        StalkerAssert.Ok(s.DoAction("Add-Trade PfName=Test SRef=NASDAQ$MSFT Date=2024-06-01 Units=10 Price=150 Fee=5 TradeId=T001 OptPurhaceId=H001 CurrencyRate=1 Note=Profit"));
        Assert.Empty(s.PortfolioHoldings("Test", "NASDAQ$MSFT"));
        Assert.Single(s.PortfolioTrades("Test", "NASDAQ$MSFT"));
        var trade = s.PortfolioTrades("Test")[0];
        Assert.Equal("T001", trade.Sold.TradeId);
        Assert.Equal(150m, trade.Sold.PricePerUnit);
    }

    [Fact]
    public void Trade_PartialSale_ReducesHolding()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$MSFT PurhaceId=H001 Date=2024-01-15 Units=10 Price=100 Fee=0 CurrencyRate=1 Note="));
        StalkerAssert.Ok(s.DoAction("Add-Trade PfName=Test SRef=NASDAQ$MSFT Date=2024-06-01 Units=3 Price=150 Fee=0 TradeId=T001 OptPurhaceId=H001 CurrencyRate=1 Note="));
        Assert.Equal(7m, s.PortfolioHoldings("Test")[0].Units);
        Assert.Equal(3m, s.PortfolioTrades("Test")[0].Units);
    }

    [Fact]
    public void Trade_FIFO_SellsOldestFirst()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$MSFT PurhaceId=H001 Date=2023-01-15 Units=5 Price=100 Fee=0 CurrencyRate=1 Note=Oldest"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$MSFT PurhaceId=H002 Date=2024-01-15 Units=5 Price=200 Fee=0 CurrencyRate=1 Note=Newest"));
        // Sell 5 with FIFO (empty OptPurhaceId)
        StalkerAssert.Ok(s.DoAction("Add-Trade PfName=Test SRef=NASDAQ$MSFT Date=2024-06-01 Units=5 Price=300 Fee=0 TradeId=T001 OptPurhaceId= CurrencyRate=1 Note="));
        // Oldest should be sold
        var trades = s.PortfolioTrades("Test");
        Assert.Single(trades);
        Assert.Equal("H001", trades[0].PurhaceId);
        // Only newest remains
        var holdings = s.PortfolioHoldings("Test");
        Assert.Single(holdings);
        Assert.Equal("H002", holdings[0].PurhaceId);
    }

    [Fact]
    public void Trade_FIFO_AcrossMultipleHoldings()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$MSFT PurhaceId=H001 Date=2023-01-15 Units=3 Price=100 Fee=0 CurrencyRate=1 Note="));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$MSFT PurhaceId=H002 Date=2023-06-15 Units=3 Price=150 Fee=0 CurrencyRate=1 Note="));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$MSFT PurhaceId=H003 Date=2024-01-15 Units=4 Price=200 Fee=0 CurrencyRate=1 Note="));
        // Sell 7 via FIFO = all of H001 (3) + all of H002 (3) + 1 from H003
        StalkerAssert.Ok(s.DoAction("Add-Trade PfName=Test SRef=NASDAQ$MSFT Date=2024-06-01 Units=7 Price=300 Fee=0 TradeId=T001 OptPurhaceId= CurrencyRate=1 Note="));
        Assert.Single(s.PortfolioHoldings("Test"));
        Assert.Equal("H003", s.PortfolioHoldings("Test")[0].PurhaceId);
        Assert.Equal(3m, s.PortfolioHoldings("Test")[0].Units);
    }

    [Fact]
    public void Trade_Overselling_Fails()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$MSFT PurhaceId=H001 Date=2024-01-15 Units=10 Price=100 Fee=0 CurrencyRate=1 Note="));
        StalkerAssert.Fail(s.DoAction("Add-Trade PfName=Test SRef=NASDAQ$MSFT Date=2024-06-01 Units=15 Price=150 Fee=0 TradeId=T001 OptPurhaceId= CurrencyRate=1 Note="), "UnitMismatch");
    }

    [Fact]
    public void Trade_SpecificHolding_Overselling_Fails()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$MSFT PurhaceId=H001 Date=2024-01-15 Units=10 Price=100 Fee=0 CurrencyRate=1 Note="));
        StalkerAssert.Fail(s.DoAction("Add-Trade PfName=Test SRef=NASDAQ$MSFT Date=2024-06-01 Units=15 Price=150 Fee=0 TradeId=T001 OptPurhaceId=H001 CurrencyRate=1 Note="), "UnitMismatch");
    }

    [Fact]
    public void Trade_DuplicateTradeId_Fails()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$MSFT PurhaceId=H001 Date=2024-01-15 Units=10 Price=100 Fee=0 CurrencyRate=1 Note="));
        StalkerAssert.Ok(s.DoAction("Add-Trade PfName=Test SRef=NASDAQ$MSFT Date=2024-06-01 Units=3 Price=150 Fee=0 TradeId=T001 OptPurhaceId=H001 CurrencyRate=1 Note="));
        StalkerAssert.Fail(s.DoAction("Add-Trade PfName=Test SRef=NASDAQ$MSFT Date=2024-07-01 Units=3 Price=160 Fee=0 TradeId=T001 OptPurhaceId=H001 CurrencyRate=1 Note="), "Duplicate");
    }

    [Fact]
    public void Trade_Delete_ReturnsUnitsToHolding()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$MSFT PurhaceId=H001 Date=2024-01-15 Units=10 Price=100 Fee=0 CurrencyRate=1 Note="));
        StalkerAssert.Ok(s.DoAction("Add-Trade PfName=Test SRef=NASDAQ$MSFT Date=2024-06-01 Units=3 Price=150 Fee=0 TradeId=T001 OptPurhaceId=H001 CurrencyRate=1 Note="));
        Assert.Equal(7m, s.PortfolioHoldings("Test")[0].Units);
        StalkerAssert.Ok(s.DoAction("Delete-Trade TradeId=T001"));
        Assert.Equal(10m, s.PortfolioHoldings("Test")[0].Units);
        Assert.Empty(s.PortfolioTrades("Test"));
    }

    [Fact]
    public void Trade_Delete_FullSale_ReturnsToHoldings()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$MSFT PurhaceId=H001 Date=2024-01-15 Units=10 Price=100 Fee=0 CurrencyRate=1 Note="));
        StalkerAssert.Ok(s.DoAction("Add-Trade PfName=Test SRef=NASDAQ$MSFT Date=2024-06-01 Units=10 Price=150 Fee=0 TradeId=T001 OptPurhaceId=H001 CurrencyRate=1 Note="));
        Assert.Empty(s.PortfolioHoldings("Test"));
        StalkerAssert.Ok(s.DoAction("Delete-Trade TradeId=T001"));
        Assert.Single(s.PortfolioHoldings("Test"));
        Assert.Equal(10m, s.PortfolioHoldings("Test")[0].Units);
    }

    [Fact]
    public void Trade_Note_UpdatesNote()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$MSFT PurhaceId=H001 Date=2024-01-15 Units=10 Price=100 Fee=0 CurrencyRate=1 Note="));
        StalkerAssert.Ok(s.DoAction("Add-Trade PfName=Test SRef=NASDAQ$MSFT Date=2024-06-01 Units=10 Price=150 Fee=0 TradeId=T001 OptPurhaceId=H001 CurrencyRate=1 Note=Original"));
        StalkerAssert.Ok(s.DoAction("Note-Trade TradeId=T001 Note=Updated"));
        Assert.Equal("Updated", s.PortfolioTrades("Test")[0].Sold.TradeNote);
    }

    [Fact]
    public void Trade_Delete_BlockedByNewerTrade()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$MSFT PurhaceId=H001 Date=2024-01-15 Units=10 Price=100 Fee=0 CurrencyRate=1 Note="));
        // First partial sale
        StalkerAssert.Ok(s.DoAction("Add-Trade PfName=Test SRef=NASDAQ$MSFT Date=2024-06-01 Units=3 Price=150 Fee=0 TradeId=T001 OptPurhaceId=H001 CurrencyRate=1 Note="));
        // Second partial sale from same holding
        StalkerAssert.Ok(s.DoAction("Add-Trade PfName=Test SRef=NASDAQ$MSFT Date=2024-09-01 Units=3 Price=200 Fee=0 TradeId=T002 OptPurhaceId=H001 CurrencyRate=1 Note="));
        // Can't delete T001 because T002 is newer
        StalkerAssert.Fail(s.DoAction("Delete-Trade TradeId=T001"), "newer");
    }
}
