using Pfs.Types;
using PfsData.Tests.Helpers;
using Xunit;

namespace PfsData.Tests.Tests;

public class HoldingTests
{
    [Fact]
    public void Add_Holding_Success()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$MSFT PurhaceId=H001 Date=2024-01-15 Units=10 Price=380.50 Fee=9.95 CurrencyRate=1.35 Note=TestBuy"));
        var holdings = s.PortfolioHoldings("Test");
        Assert.Single(holdings);
        Assert.Equal("H001", holdings[0].PurhaceId);
        Assert.Equal(10m, holdings[0].Units);
        Assert.Equal(10m, holdings[0].OriginalUnits);
    }

    [Fact]
    public void Add_DuplicatePurhaceId_Fails()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$MSFT PurhaceId=H001 Date=2024-01-15 Units=10 Price=380.50 Fee=0 CurrencyRate=1 Note=First"));
        StalkerAssert.Fail(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$MSFT PurhaceId=H001 Date=2024-02-15 Units=5 Price=390.00 Fee=0 CurrencyRate=1 Note=Second"), "Duplicate");
    }

    [Fact]
    public void Add_Holding_FeePerUnitCalculated()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$MSFT PurhaceId=H001 Date=2024-01-15 Units=10 Price=100 Fee=10 CurrencyRate=1 Note="));
        var h = s.PortfolioHoldings("Test")[0];
        Assert.Equal(1m, h.FeePerUnit);
    }

    [Fact]
    public void Edit_Holding_Success()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$MSFT PurhaceId=H001 Date=2024-01-15 Units=10 Price=100 Fee=0 CurrencyRate=1 Note="));
        StalkerAssert.Ok(s.DoAction("Edit-Holding PurhaceId=H001 Date=2024-01-20 Units=15 Price=105 Fee=0 CurrencyRate=1.1 Note=Edited"));
        var h = s.PortfolioHoldings("Test")[0];
        Assert.Equal(15m, h.Units);
        Assert.Equal(105m, h.PricePerUnit);
        Assert.Equal("Edited", h.PurhaceNote);
    }

    [Fact]
    public void Edit_Holding_AfterTrade_Fails()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$MSFT PurhaceId=H001 Date=2024-01-15 Units=10 Price=100 Fee=0 CurrencyRate=1 Note="));
        StalkerAssert.Ok(s.DoAction("Add-Trade PfName=Test SRef=NASDAQ$MSFT Date=2024-06-01 Units=5 Price=150 Fee=0 TradeId=T001 OptPurhaceId=H001 CurrencyRate=1 Note="));
        StalkerAssert.Fail(s.DoAction("Edit-Holding PurhaceId=H001 Date=2024-01-20 Units=10 Price=105 Fee=0 CurrencyRate=1 Note="), "already sold");
    }

    [Fact]
    public void Note_Holding_UpdatesNote()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$MSFT PurhaceId=H001 Date=2024-01-15 Units=10 Price=100 Fee=0 CurrencyRate=1 Note=Original"));
        StalkerAssert.Ok(s.DoAction("Note-Holding PurhaceId=H001 Note=Updated"));
        Assert.Equal("Updated", s.PortfolioHoldings("Test")[0].PurhaceNote);
    }

    [Fact]
    public void Delete_Holding_Success()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$MSFT PurhaceId=H001 Date=2024-01-15 Units=10 Price=100 Fee=0 CurrencyRate=1 Note="));
        StalkerAssert.Ok(s.DoAction("Delete-Holding PurhaceId=H001"));
        Assert.Empty(s.PortfolioHoldings("Test"));
    }

    [Fact]
    public void Delete_Holding_WithTrades_Fails()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$MSFT PurhaceId=H001 Date=2024-01-15 Units=10 Price=100 Fee=0 CurrencyRate=1 Note="));
        StalkerAssert.Ok(s.DoAction("Add-Trade PfName=Test SRef=NASDAQ$MSFT Date=2024-06-01 Units=5 Price=150 Fee=0 TradeId=T001 OptPurhaceId=H001 CurrencyRate=1 Note="));
        StalkerAssert.Fail(s.DoAction("Delete-Holding PurhaceId=H001"), "Trades");
    }

    [Fact]
    public void Delete_Holding_WithDividents_Fails()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NYSE$KO PurhaceId=H001 Date=2024-01-15 Units=10 Price=60 Fee=0 CurrencyRate=1 Note="));
        StalkerAssert.Ok(s.DoAction("Add-Divident PfName=Test SRef=NYSE$KO OptPurhaceId= OptTradeId= ExDivDate=2024-03-01 PaymentDate=2024-04-01 Units=10 PaymentPerUnit=0.48 CurrencyRate=1 Currency=USD"));
        StalkerAssert.Fail(s.DoAction("Delete-Holding PurhaceId=H001"), "Dividents");
    }

    [Fact]
    public void Round_Holding_TruncatesDecimals()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$MSFT PurhaceId=H001 Date=2024-01-15 Units=10.75 Price=100 Fee=0 CurrencyRate=1 Note="));
        StalkerAssert.Ok(s.DoAction("Round-Holding PfName=Test SRef=NASDAQ$MSFT Units=10.75"));
        Assert.Equal(10m, s.PortfolioHoldings("Test")[0].Units);
    }
}
