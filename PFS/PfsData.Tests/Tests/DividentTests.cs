using Pfs.Types;
using PfsData.Tests.Helpers;
using Xunit;

namespace PfsData.Tests.Tests;

public class DividentTests
{
    [Fact]
    public void Add_Divident_AutoAssign_Success()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NYSE$KO PurhaceId=H001 Date=2024-01-15 Units=50 Price=60 Fee=0 CurrencyRate=1 Note="));
        StalkerAssert.Ok(s.DoAction("Add-Divident PfName=Test SRef=NYSE$KO OptPurhaceId= OptTradeId= ExDivDate=2024-03-01 PaymentDate=2024-04-01 Units=50 PaymentPerUnit=0.48 CurrencyRate=1 Currency=USD"));
        Assert.Single(s.PortfolioHoldings("Test")[0].Dividents);
        Assert.Equal(0.48m, s.PortfolioHoldings("Test")[0].Dividents[0].PaymentPerUnit);
    }

    [Fact]
    public void Add_Divident_ExcludesRecentPurchase()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NYSE$KO PurhaceId=H001 Date=2024-01-15 Units=50 Price=60 Fee=0 CurrencyRate=1 Note=OldBuy"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NYSE$KO PurhaceId=H002 Date=2024-03-01 Units=20 Price=62 Fee=0 CurrencyRate=1 Note=NewBuy"));
        // ExDivDate=2024-03-01, H002 bought on same day should be excluded (bought < exDivDate is requirement)
        StalkerAssert.Ok(s.DoAction("Add-Divident PfName=Test SRef=NYSE$KO OptPurhaceId= OptTradeId= ExDivDate=2024-03-01 PaymentDate=2024-04-01 Units=50 PaymentPerUnit=0.48 CurrencyRate=1 Currency=USD"));
        Assert.Single(s.PortfolioHoldings("Test", "NYSE$KO")[0].Dividents);  // H001 gets divident
        Assert.Empty(s.PortfolioHoldings("Test", "NYSE$KO")[1].Dividents);   // H002 excluded
    }

    [Fact]
    public void Add_Divident_UnitMismatch_Fails()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NYSE$KO PurhaceId=H001 Date=2024-01-15 Units=50 Price=60 Fee=0 CurrencyRate=1 Note="));
        StalkerAssert.Fail(s.DoAction("Add-Divident PfName=Test SRef=NYSE$KO OptPurhaceId= OptTradeId= ExDivDate=2024-03-01 PaymentDate=2024-04-01 Units=100 PaymentPerUnit=0.48 CurrencyRate=1 Currency=USD"), "UnitMismatch");
    }

    [Fact]
    public void Add_Divident_DuplicateExDivDate_Fails()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NYSE$KO PurhaceId=H001 Date=2024-01-15 Units=50 Price=60 Fee=0 CurrencyRate=1 Note="));
        StalkerAssert.Ok(s.DoAction("Add-Divident PfName=Test SRef=NYSE$KO OptPurhaceId= OptTradeId= ExDivDate=2024-03-01 PaymentDate=2024-04-01 Units=50 PaymentPerUnit=0.48 CurrencyRate=1 Currency=USD"));
        StalkerAssert.Fail(s.DoAction("Add-Divident PfName=Test SRef=NYSE$KO OptPurhaceId= OptTradeId= ExDivDate=2024-03-01 PaymentDate=2024-04-15 Units=50 PaymentPerUnit=0.50 CurrencyRate=1 Currency=USD"), "Duplicate");
    }

    [Fact]
    public void Add_Divident_TargetedToPurhaceId()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NYSE$KO PurhaceId=H001 Date=2024-01-15 Units=30 Price=60 Fee=0 CurrencyRate=1 Note="));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NYSE$KO PurhaceId=H002 Date=2024-02-01 Units=20 Price=62 Fee=0 CurrencyRate=1 Note="));
        // Only target H001
        StalkerAssert.Ok(s.DoAction("Add-Divident PfName=Test SRef=NYSE$KO OptPurhaceId=H001 OptTradeId= ExDivDate=2024-06-01 PaymentDate=2024-07-01 Units=30 PaymentPerUnit=0.48 CurrencyRate=1 Currency=USD"));
        Assert.Single(s.PortfolioHoldings("Test", "NYSE$KO")[0].Dividents);
        Assert.Empty(s.PortfolioHoldings("Test", "NYSE$KO")[1].Dividents);
    }

    [Fact]
    public void Add_Divident_OnTrade_SoldAfterExDiv()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NYSE$KO PurhaceId=H001 Date=2024-01-15 Units=50 Price=60 Fee=0 CurrencyRate=1 Note="));
        StalkerAssert.Ok(s.DoAction("Add-Trade PfName=Test SRef=NYSE$KO Date=2024-04-01 Units=50 Price=65 Fee=0 TradeId=T001 OptPurhaceId=H001 CurrencyRate=1 Note="));
        // Sold after ExDivDate so should receive dividend on the trade
        StalkerAssert.Ok(s.DoAction("Add-Divident PfName=Test SRef=NYSE$KO OptPurhaceId= OptTradeId= ExDivDate=2024-03-01 PaymentDate=2024-04-15 Units=50 PaymentPerUnit=0.48 CurrencyRate=1 Currency=USD"));
        Assert.Single(s.PortfolioTrades("Test")[0].Dividents);
    }

    [Fact]
    public void Add_Divident_OnTrade_SoldBeforeExDiv_Excluded()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NYSE$KO PurhaceId=H001 Date=2024-01-15 Units=50 Price=60 Fee=0 CurrencyRate=1 Note="));
        StalkerAssert.Ok(s.DoAction("Add-Trade PfName=Test SRef=NYSE$KO Date=2024-02-15 Units=50 Price=65 Fee=0 TradeId=T001 OptPurhaceId=H001 CurrencyRate=1 Note="));
        // Sold before ExDivDate, units should not match
        StalkerAssert.Fail(s.DoAction("Add-Divident PfName=Test SRef=NYSE$KO OptPurhaceId= OptTradeId= ExDivDate=2024-03-01 PaymentDate=2024-04-15 Units=50 PaymentPerUnit=0.48 CurrencyRate=1 Currency=USD"), "UnitMismatch");
    }

    [Fact]
    public void DeleteAll_Divident_RemovesFromHoldingsAndTrades()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NYSE$KO PurhaceId=H001 Date=2024-01-15 Units=50 Price=60 Fee=0 CurrencyRate=1 Note="));
        StalkerAssert.Ok(s.DoAction("Add-Divident PfName=Test SRef=NYSE$KO OptPurhaceId= OptTradeId= ExDivDate=2024-03-01 PaymentDate=2024-04-01 Units=50 PaymentPerUnit=0.48 CurrencyRate=1 Currency=USD"));
        Assert.Single(s.PortfolioHoldings("Test")[0].Dividents);
        StalkerAssert.Ok(s.DoAction("DeleteAll-Divident PfName=Test SRef=NYSE$KO ExDivDate=2024-03-01"));
        Assert.Empty(s.PortfolioHoldings("Test")[0].Dividents);
    }
}
