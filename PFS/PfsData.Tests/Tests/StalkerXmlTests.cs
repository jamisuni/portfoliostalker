using Pfs.Data.Stalker;
using Pfs.Types;
using PfsData.Tests.Helpers;
using Xunit;

namespace PfsData.Tests.Tests;

public class StalkerXmlTests
{
    [Fact]
    public void ExportXml_ProducesValidXml()
    {
        var s = StalkerTestFixture.CreatePopulated();
        string xml = StalkerXML.ExportXml(s);
        Assert.Contains("<PFS>", xml);
        Assert.Contains("<Portfolios>", xml);
        Assert.Contains("<Stocks>", xml);
        Assert.Contains("<Sectors>", xml);
    }

    [Fact]
    public void ImportXml_ParsesWithoutWarnings()
    {
        var s = StalkerTestFixture.CreatePopulated();
        string xml = StalkerXML.ExportXml(s);
        var (data, warnings) = StalkerXML.ImportXml(xml);
        Assert.Empty(warnings);
        Assert.NotNull(data);
    }

    [Fact]
    public void RoundTrip_PortfoliosPreserved()
    {
        var s = StalkerTestFixture.CreatePopulated();
        string xml = StalkerXML.ExportXml(s);
        var (data, _) = StalkerXML.ImportXml(xml);
        Assert.Equal(s.Portfolios().Count, data.Portfolios.Count);
        foreach (var origPf in s.Portfolios())
            Assert.Contains(data.Portfolios, p => p.Name == origPf.Name);
    }

    [Fact]
    public void RoundTrip_HoldingsPreserved()
    {
        var s = StalkerTestFixture.CreatePopulated();
        string xml = StalkerXML.ExportXml(s);
        var (data, _) = StalkerXML.ImportXml(xml);
        var origMain = s.PortfolioHoldings("Main");
        var importedMain = data.Portfolios.Single(p => p.Name == "Main").StockHoldings;
        Assert.Equal(origMain.Count, importedMain.Count);
        // Verify a specific holding
        var origH = origMain.Single(h => h.PurhaceId == "M001");
        var impH = importedMain.Single(h => h.PurhaceId == "M001");
        Assert.Equal(origH.Units, impH.Units);
        Assert.Equal(origH.PricePerUnit, impH.PricePerUnit);
        Assert.Equal(origH.PurhaceDate, impH.PurhaceDate);
    }

    [Fact]
    public void RoundTrip_AlarmsPreserved()
    {
        var s = StalkerTestFixture.CreatePopulated();
        string xml = StalkerXML.ExportXml(s);
        var (data, _) = StalkerXML.ImportXml(xml);
        var origMsft = s.StockRef("NASDAQ$MSFT");
        var impMsft = data.Stocks.Single(st => st.SRef == "NASDAQ$MSFT");
        Assert.Equal(origMsft.Alarms.Count, impMsft.Alarms.Count);
        Assert.Equal(origMsft.Alarms[0].Level, impMsft.Alarms[0].Level);
        Assert.Equal(origMsft.Alarms[0].AlarmType, impMsft.Alarms[0].AlarmType);
    }

    [Fact]
    public void RoundTrip_SectorsPreserved()
    {
        var s = StalkerTestFixture.CreatePopulated();
        string xml = StalkerXML.ExportXml(s);
        var (data, _) = StalkerXML.ImportXml(xml);
        Assert.NotNull(data.Sectors[0]);
        Assert.Equal("Industry", data.Sectors[0].Name);
        Assert.Equal("Tech", data.Sectors[0].FieldNames[0]);
        Assert.Equal("Energy", data.Sectors[0].FieldNames[1]);
    }

    [Fact]
    public void RoundTrip_OrdersPreserved()
    {
        var s = StalkerTestFixture.CreatePopulated();
        string xml = StalkerXML.ExportXml(s);
        var (data, warnings) = StalkerXML.ImportXml(xml);
        Assert.Empty(warnings);
        var mainPf = data.Portfolios.Single(p => p.Name == "Main");
        var msftOrder = mainPf.StockOrders.Single(o => o.SRef == "NASDAQ$MSFT");
        Assert.Equal(SOrder.OrderType.Buy, msftOrder.Type);
        Assert.Equal(250.00m, msftOrder.PricePerUnit);
        Assert.Equal(5m, msftOrder.Units);
        var growthPf = data.Portfolios.Single(p => p.Name == "Growth");
        var nvdaOrder = growthPf.StockOrders.Single(o => o.SRef == "NASDAQ$NVDA");
        Assert.Equal(SOrder.OrderType.Sell, nvdaOrder.Type);
        Assert.Equal(900.00m, nvdaOrder.PricePerUnit);
    }

    [Fact]
    public void RoundTrip_TradesPreserved()
    {
        var s = StalkerTestFixture.CreatePopulated();
        string xml = StalkerXML.ExportXml(s);
        var (data, warnings) = StalkerXML.ImportXml(xml);
        Assert.Empty(warnings);
        var growthPf = data.Portfolios.Single(p => p.Name == "Growth");
        var nvdaTrades = growthPf.StockTrades.Where(t => t.SRef == "NASDAQ$NVDA").ToList();
        Assert.Single(nvdaTrades);
        Assert.Equal("N001", nvdaTrades[0].PurhaceId);
        Assert.Equal("T001", nvdaTrades[0].Sold.TradeId);
        Assert.Equal(450.00m, nvdaTrades[0].Sold.PricePerUnit);
        Assert.Equal(5m, nvdaTrades[0].Units);
    }

    [Fact]
    public void RoundTrip_DividentsPreserved()
    {
        var s = StalkerTestFixture.CreatePopulated();
        string xml = StalkerXML.ExportXml(s);
        var (data, warnings) = StalkerXML.ImportXml(xml);
        Assert.Empty(warnings);
        var mainPf = data.Portfolios.Single(p => p.Name == "Main");
        var koHolding = mainPf.StockHoldings.Single(h => h.PurhaceId == "K001");
        Assert.Equal(3, koHolding.Dividents.Count);
        Assert.Equal(0.485m, koHolding.Dividents[0].PaymentPerUnit);
        Assert.Equal(new DateOnly(2024, 3, 1), koHolding.Dividents[0].ExDivDate);
        Assert.Equal(CurrencyId.USD, koHolding.Dividents[0].Currency);
    }

    [Fact]
    public void RoundTrip_SectorStockAssignmentsPreserved()
    {
        var s = StalkerTestFixture.CreatePopulated();
        string xml = StalkerXML.ExportXml(s);
        var (data, warnings) = StalkerXML.ImportXml(xml);
        Assert.Empty(warnings);
        // MSFT should have sector 0=Tech AND sector 1=NorthAmerica
        var msft = data.Stocks.Single(st => st.SRef == "NASDAQ$MSFT");
        Assert.True(msft.Sectors[0] >= 0);
        Assert.Equal("Tech", data.Sectors[0].FieldNames[msft.Sectors[0]]);
        Assert.True(msft.Sectors[1] >= 0);
        Assert.Equal("NorthAmerica", data.Sectors[1].FieldNames[msft.Sectors[1]]);
        // SAP should have sector 0=unset, sector 1=Europe
        var sap = data.Stocks.Single(st => st.SRef == "XETRA$SAP");
        Assert.Equal(-1, sap.Sectors[0]);
        Assert.True(sap.Sectors[1] >= 0);
        Assert.Equal("Europe", data.Sectors[1].FieldNames[sap.Sectors[1]]);
    }

    [Fact]
    public void RoundTrip_CurrencyRatePreserved()
    {
        var s = StalkerTestFixture.CreatePopulated();
        string xml = StalkerXML.ExportXml(s);
        var (data, warnings) = StalkerXML.ImportXml(xml);
        Assert.Empty(warnings);
        var mainPf = data.Portfolios.Single(p => p.Name == "Main");
        var m001 = mainPf.StockHoldings.Single(h => h.PurhaceId == "M001");
        Assert.Equal(1.35m, m001.CurrencyRate);
    }
}
