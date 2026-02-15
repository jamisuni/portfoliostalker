using Pfs.Data.Stalker;
using Pfs.Types;
using PfsData.Tests.Helpers;
using Xunit;

namespace PfsData.Tests.Tests;

public class RealDataPatternTests
{
    [Fact]
    public void Symbol_WithDot_Accepted()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Alarm Type=Under SRef=NYSE$BF.B Level=50.00 Prms= Note=Whiskey"));
        Assert.NotNull(s.StockRef("NYSE$BF.B"));
    }

    [Fact]
    public void Symbol_WithDash_Accepted()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Alarm Type=Under SRef=TSX$DIR-UN Level=10.00 Prms= Note=REIT"));
        Assert.NotNull(s.StockRef("TSX$DIR-UN"));
    }

    [Fact]
    public void CustomPurhaceId_AlphanumericFormats()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$MSFT PurhaceId=A20230413 Date=2023-04-13 Units=10 Price=100 Fee=0 CurrencyRate=1 Note="));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$MSFT PurhaceId=mafromemx Date=2023-05-01 Units=5 Price=110 Fee=0 CurrencyRate=1 Note="));
        Assert.Equal(2, s.PortfolioHoldings("Test").Count);
        Assert.Equal("A20230413", s.PortfolioHoldings("Test")[0].PurhaceId);
        Assert.Equal("mafromemx", s.PortfolioHoldings("Test")[1].PurhaceId);
    }

    [Fact]
    public void MultipleDividendsOnOneHolding()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NYSE$KO PurhaceId=H001 Date=2023-01-15 Units=100 Price=60 Fee=0 CurrencyRate=1 Note="));
        // 4 quarterly dividends
        StalkerAssert.Ok(s.DoAction("Add-Divident PfName=Test SRef=NYSE$KO OptPurhaceId= OptTradeId= ExDivDate=2024-03-01 PaymentDate=2024-04-01 Units=100 PaymentPerUnit=0.48 CurrencyRate=1 Currency=USD"));
        StalkerAssert.Ok(s.DoAction("Add-Divident PfName=Test SRef=NYSE$KO OptPurhaceId= OptTradeId= ExDivDate=2024-06-01 PaymentDate=2024-07-01 Units=100 PaymentPerUnit=0.48 CurrencyRate=1 Currency=USD"));
        StalkerAssert.Ok(s.DoAction("Add-Divident PfName=Test SRef=NYSE$KO OptPurhaceId= OptTradeId= ExDivDate=2024-09-01 PaymentDate=2024-10-01 Units=100 PaymentPerUnit=0.48 CurrencyRate=1 Currency=USD"));
        StalkerAssert.Ok(s.DoAction("Add-Divident PfName=Test SRef=NYSE$KO OptPurhaceId= OptTradeId= ExDivDate=2024-12-01 PaymentDate=2025-01-01 Units=100 PaymentPerUnit=0.485 CurrencyRate=1 Currency=USD"));
        Assert.Equal(4, s.PortfolioHoldings("Test")[0].Dividents.Count);
    }

    [Fact]
    public void DividentOnTrade_MultipleScenarios()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NYSE$KO PurhaceId=H001 Date=2023-01-15 Units=100 Price=60 Fee=0 CurrencyRate=1 Note="));
        // Add dividend before sale
        StalkerAssert.Ok(s.DoAction("Add-Divident PfName=Test SRef=NYSE$KO OptPurhaceId= OptTradeId= ExDivDate=2024-03-01 PaymentDate=2024-04-01 Units=100 PaymentPerUnit=0.48 CurrencyRate=1 Currency=USD"));
        // Partial sale (50 units sold after ExDivDate)
        StalkerAssert.Ok(s.DoAction("Add-Trade PfName=Test SRef=NYSE$KO Date=2024-06-01 Units=50 Price=65 Fee=0 TradeId=T001 OptPurhaceId=H001 CurrencyRate=1 Note="));
        // Add dividend after sale - goes to both holding (50 units) and trade (50 units, sold after exdiv)
        StalkerAssert.Ok(s.DoAction("Add-Divident PfName=Test SRef=NYSE$KO OptPurhaceId= OptTradeId= ExDivDate=2024-05-01 PaymentDate=2024-06-15 Units=100 PaymentPerUnit=0.48 CurrencyRate=1 Currency=USD"));
        Assert.Equal(2, s.PortfolioHoldings("Test")[0].Dividents.Count);
        // Trade gets both dividends: the one from before the sale (transferred when split) + the one added after
        Assert.Equal(2, s.PortfolioTrades("Test")[0].Dividents.Count);
    }

    [Fact]
    public void MultiplePartialSales_SameHolding()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$MSFT PurhaceId=H001 Date=2023-01-15 Units=100 Price=100 Fee=0 CurrencyRate=1 Note="));
        // 3 partial sales: 20, 30, 50
        StalkerAssert.Ok(s.DoAction("Add-Trade PfName=Test SRef=NASDAQ$MSFT Date=2024-03-01 Units=20 Price=150 Fee=0 TradeId=T001 OptPurhaceId=H001 CurrencyRate=1 Note="));
        Assert.Equal(80m, s.PortfolioHoldings("Test")[0].Units);
        StalkerAssert.Ok(s.DoAction("Add-Trade PfName=Test SRef=NASDAQ$MSFT Date=2024-06-01 Units=30 Price=160 Fee=0 TradeId=T002 OptPurhaceId=H001 CurrencyRate=1 Note="));
        Assert.Equal(50m, s.PortfolioHoldings("Test")[0].Units);
        StalkerAssert.Ok(s.DoAction("Add-Trade PfName=Test SRef=NASDAQ$MSFT Date=2024-09-01 Units=50 Price=170 Fee=0 TradeId=T003 OptPurhaceId=H001 CurrencyRate=1 Note="));
        // All sold - holding moves to trades
        Assert.Empty(s.PortfolioHoldings("Test", "NASDAQ$MSFT"));
        Assert.Equal(3, s.PortfolioTrades("Test", "NASDAQ$MSFT").Count);
    }

    [Fact]
    public void DeleteDivident_FromSpecificHolding()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NYSE$KO PurhaceId=H001 Date=2023-01-15 Units=50 Price=60 Fee=0 CurrencyRate=1 Note="));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NYSE$KO PurhaceId=H002 Date=2023-06-01 Units=50 Price=62 Fee=0 CurrencyRate=1 Note="));
        // Add dividend to both holdings
        StalkerAssert.Ok(s.DoAction("Add-Divident PfName=Test SRef=NYSE$KO OptPurhaceId= OptTradeId= ExDivDate=2024-03-01 PaymentDate=2024-04-01 Units=100 PaymentPerUnit=0.48 CurrencyRate=1 Currency=USD"));
        Assert.Single(s.PortfolioHoldings("Test", "NYSE$KO")[0].Dividents);
        Assert.Single(s.PortfolioHoldings("Test", "NYSE$KO")[1].Dividents);
        // Delete dividend only from H001
        StalkerAssert.Ok(s.DoAction("Delete-Divident PfName=Test SRef=NYSE$KO ExDivDate=2024-03-01 PurhaceId=H001"));
        Assert.Empty(s.PortfolioHoldings("Test", "NYSE$KO")[0].Dividents);
        Assert.Single(s.PortfolioHoldings("Test", "NYSE$KO")[1].Dividents);  // H002 still has it
    }

    [Fact]
    public void DeleteDivident_FromTrade()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NYSE$KO PurhaceId=H001 Date=2023-01-15 Units=50 Price=60 Fee=0 CurrencyRate=1 Note="));
        // Sell all, then add dividend on the trade (sold after exdiv)
        StalkerAssert.Ok(s.DoAction("Add-Trade PfName=Test SRef=NYSE$KO Date=2024-06-01 Units=50 Price=65 Fee=0 TradeId=T001 OptPurhaceId=H001 CurrencyRate=1 Note="));
        StalkerAssert.Ok(s.DoAction("Add-Divident PfName=Test SRef=NYSE$KO OptPurhaceId= OptTradeId= ExDivDate=2024-03-01 PaymentDate=2024-04-01 Units=50 PaymentPerUnit=0.48 CurrencyRate=1 Currency=USD"));
        Assert.Single(s.PortfolioTrades("Test")[0].Dividents);
        // Delete dividend using H001 purhaceId (trade still has same purhaceId)
        StalkerAssert.Ok(s.DoAction("Delete-Divident PfName=Test SRef=NYSE$KO ExDivDate=2024-03-01 PurhaceId=H001"));
        Assert.Empty(s.PortfolioTrades("Test")[0].Dividents);
    }

    [Fact]
    public void ZeroFeeHolding()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$MSFT PurhaceId=H001 Date=2024-01-15 Units=10 Price=100 Fee=0 CurrencyRate=1 Note="));
        Assert.Equal(0m, s.PortfolioHoldings("Test")[0].FeePerUnit);
    }

    [Fact]
    public void ZeroFeeTrade()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$MSFT PurhaceId=H001 Date=2024-01-15 Units=10 Price=100 Fee=0 CurrencyRate=1 Note="));
        StalkerAssert.Ok(s.DoAction("Add-Trade PfName=Test SRef=NASDAQ$MSFT Date=2024-06-01 Units=10 Price=150 Fee=0 TradeId=T001 OptPurhaceId=H001 CurrencyRate=1 Note="));
        Assert.Equal(0m, s.PortfolioTrades("Test")[0].Sold.FeePerUnit);
    }

    [Fact]
    public void ThreeSectorsSimultaneously()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Set-Sector SectorId=0 SectorName=Industry"));
        StalkerAssert.Ok(s.DoAction("Edit-Sector SectorId=0 FieldId=0 FieldName=Tech"));
        StalkerAssert.Ok(s.DoAction("Set-Sector SectorId=1 SectorName=Geography"));
        StalkerAssert.Ok(s.DoAction("Edit-Sector SectorId=1 FieldId=0 FieldName=USA"));
        StalkerAssert.Ok(s.DoAction("Set-Sector SectorId=2 SectorName=Size"));
        StalkerAssert.Ok(s.DoAction("Edit-Sector SectorId=2 FieldId=0 FieldName=Large"));
        StalkerAssert.Ok(s.DoAction("Follow-Sector SRef=NASDAQ$MSFT SectorId=0 FieldId=0"));
        StalkerAssert.Ok(s.DoAction("Follow-Sector SRef=NASDAQ$MSFT SectorId=1 FieldId=0"));
        StalkerAssert.Ok(s.DoAction("Follow-Sector SRef=NASDAQ$MSFT SectorId=2 FieldId=0"));
        var sectors = s.GetStockSectors("NASDAQ$MSFT");
        Assert.Equal("Tech", sectors[0]);
        Assert.Equal("USA", sectors[1]);
        Assert.Equal("Large", sectors[2]);
    }

    [Fact]
    public void CloseStock_SRefBecomesClosed()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Follow-Portfolio PfName=Test SRef=NASDAQ$DEAD"));
        StalkerAssert.Ok(s.DoAction("Add-Alarm Type=Under SRef=NASDAQ$DEAD Level=5.00 Prms= Note="));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NASDAQ$DEAD PurhaceId=H001 Date=2024-01-15 Units=100 Price=10 Fee=0 CurrencyRate=1 Note="));
        StalkerAssert.Ok(s.DoAction("Close-Stock SRef=NASDAQ$DEAD Date=2024-12-01 Note=Bankrupt"));
        Assert.NotNull(s.StockRef("CLOSED$DEAD"));
        Assert.Null(s.StockRef("NASDAQ$DEAD"));
        Assert.Contains("CLOSED$DEAD", s.PortfolioSRefs("Test"));
    }

    [Fact]
    public void XmlRoundTrip_SpecialCharSymbols()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Follow-Portfolio PfName=Test SRef=NYSE$BF.B"));
        StalkerAssert.Ok(s.DoAction("Add-Alarm Type=Under SRef=NYSE$BF.B Level=50.00 Prms= Note="));
        StalkerAssert.Ok(s.DoAction("Add-Holding PfName=Test SRef=NYSE$BF.B PurhaceId=H001 Date=2024-01-15 Units=10 Price=60 Fee=0 CurrencyRate=1 Note="));
        string xml = StalkerXML.ExportXml(s);
        var (data, warnings) = StalkerXML.ImportXml(xml);
        Assert.Empty(warnings);
        Assert.Contains(data.Stocks, st => st.SRef == "NYSE$BF.B");
        var pfTest = data.Portfolios.Single(p => p.Name == "Test");
        Assert.Contains(pfTest.StockHoldings, h => h.SRef == "NYSE$BF.B");
    }
}
