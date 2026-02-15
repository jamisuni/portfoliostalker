using Pfs.Types;
using PfsData.Tests.Helpers;
using Xunit;

namespace PfsData.Tests.Tests;

public class OrderTests
{
    [Fact]
    public void Add_Order_Success()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Order PfName=Test Type=Buy SRef=NASDAQ$MSFT Units=10 Price=250.00 LastDate=2025-12-31"));
        var orders = s.PortfolioOrders("Test");
        Assert.Single(orders);
        Assert.Equal(SOrder.OrderType.Buy, orders[0].Type);
        Assert.Equal(250.00m, orders[0].PricePerUnit);
    }

    [Fact]
    public void Add_DuplicatePrice_Fails()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Order PfName=Test Type=Buy SRef=NASDAQ$MSFT Units=10 Price=250.00 LastDate=2025-12-31"));
        StalkerAssert.Fail(s.DoAction("Add-Order PfName=Test Type=Sell SRef=NASDAQ$MSFT Units=5 Price=250.00 LastDate=2025-12-31"), "same price");
    }

    [Fact]
    public void Edit_Order_Success()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Order PfName=Test Type=Buy SRef=NASDAQ$MSFT Units=10 Price=250.00 LastDate=2025-12-31"));
        StalkerAssert.Ok(s.DoAction("Edit-Order PfName=Test Type=Buy SRef=NASDAQ$MSFT EditedPrice=250.00 Units=15 Price=240.00 LastDate=2026-06-30"));
        var orders = s.PortfolioOrders("Test");
        Assert.Single(orders);
        Assert.Equal(15m, orders[0].Units);
        Assert.Equal(240.00m, orders[0].PricePerUnit);
    }

    [Fact]
    public void Delete_Order_Success()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Order PfName=Test Type=Buy SRef=NASDAQ$MSFT Units=10 Price=250.00 LastDate=2025-12-31"));
        StalkerAssert.Ok(s.DoAction("Delete-Order PfName=Test SRef=NASDAQ$MSFT Price=250.00"));
        Assert.Empty(s.PortfolioOrders("Test"));
    }

    [Fact]
    public void Delete_NonExistent_Fails()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Fail(s.DoAction("Delete-Order PfName=Test SRef=NASDAQ$MSFT Price=999.00"));
    }

    [Fact]
    public void Reset_Order_ClearsFillDate()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Order PfName=Test Type=Buy SRef=NASDAQ$MSFT Units=10 Price=250.00 LastDate=2025-12-31"));
        StalkerAssert.Ok(s.DoAction("Set-Order PfName=Test SRef=NASDAQ$MSFT Price=250.00"));
        Assert.Null(s.PortfolioOrders("Test")[0].FillDate);
    }

    [Fact]
    public void Add_SellOrder_Success()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Add-Order PfName=Test Type=Sell SRef=NYSE$KO Units=50 Price=70.00 LastDate=2025-12-31"));
        Assert.Equal(SOrder.OrderType.Sell, s.PortfolioOrders("Test")[0].Type);
    }
}
