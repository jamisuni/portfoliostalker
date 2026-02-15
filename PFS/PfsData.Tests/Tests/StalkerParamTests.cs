using Pfs.Data.Stalker;
using Pfs.Types;
using Xunit;

namespace PfsData.Tests.Tests;

public class StalkerParamTests
{
    [Fact]
    public void Parse_SRef_Valid()
    {
        var p = new StalkerParam("SRef=SRef");
        var result = p.Parse("NASDAQ$MSFT");
        Assert.True(result.Ok);
        Assert.Equal("NASDAQ$MSFT", (string)p);
    }

    [Fact]
    public void Parse_SRef_Invalid_NoMarket()
    {
        var p = new StalkerParam("SRef=SRef");
        var result = p.Parse("$MSFT");
        Assert.True(result.Fail);
    }

    [Fact]
    public void Parse_SRef_Invalid_BadMarket()
    {
        var p = new StalkerParam("SRef=SRef");
        var result = p.Parse("FAKE$MSFT");
        Assert.True(result.Fail);
    }

    [Fact]
    public void Parse_Date_Valid()
    {
        var p = new StalkerParam("Date=Date");
        var result = p.Parse("2024-01-15");
        Assert.True(result.Ok);
        Assert.Equal(new DateOnly(2024, 1, 15), (DateOnly)p);
    }

    [Fact]
    public void Parse_Date_Invalid()
    {
        var p = new StalkerParam("Date=Date");
        var result = p.Parse("15-01-2024");
        Assert.True(result.Fail);
    }

    [Fact]
    public void Parse_Decimal_InRange()
    {
        var p = new StalkerParam("Price=Decimal:0.01:1000");
        var result = p.Parse("99.50");
        Assert.True(result.Ok);
        Assert.Equal(99.50m, (decimal)p);
    }

    [Fact]
    public void Parse_Decimal_BelowMin()
    {
        var p = new StalkerParam("Price=Decimal:10:1000");
        var result = p.Parse("5");
        Assert.True(result.Fail);
    }

    [Fact]
    public void Parse_Decimal_AboveMax()
    {
        var p = new StalkerParam("Price=Decimal:0.01:100");
        var result = p.Parse("200");
        Assert.True(result.Fail);
    }

    [Fact]
    public void Parse_StockAlarmType_Valid()
    {
        var p = new StalkerParam("Type=StockAlarmType");
        var result = p.Parse("Under");
        Assert.True(result.Ok);
        Assert.Equal(SAlarmType.Under, (SAlarmType)p);
    }

    [Fact]
    public void Parse_NamedParam_WrongName()
    {
        var p = new StalkerParam("SRef=SRef");
        var result = p.Parse("Wrong=NASDAQ$MSFT");
        Assert.True(result.Fail);
    }
}
