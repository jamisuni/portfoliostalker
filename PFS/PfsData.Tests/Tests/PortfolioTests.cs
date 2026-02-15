using Pfs.Types;
using PfsData.Tests.Helpers;
using Xunit;

namespace PfsData.Tests.Tests;

public class PortfolioTests
{
    [Fact]
    public void Add_Portfolio_Success()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=TestPf"));
        Assert.Single(s.Portfolios());
        Assert.Equal("TestPf", s.Portfolios()[0].Name);
    }

    [Fact]
    public void Add_Duplicate_Fails()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=TestPf"));
        StalkerAssert.Fail(s.DoAction("Add-Portfolio PfName=TestPf"), "duplicate");
    }

    [Fact]
    public void Add_Duplicate_CaseInsensitive()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=TestPf"));
        StalkerAssert.Fail(s.DoAction("Add-Portfolio PfName=TESTPF"), "duplicate");
    }

    [Fact]
    public void Edit_Portfolio_RenameSuccess()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=OldName"));
        StalkerAssert.Ok(s.DoAction("Edit-Portfolio PfCurrName=OldName PfNewName=NewName"));
        Assert.Equal("NewName", s.Portfolios()[0].Name);
    }

    [Fact]
    public void Edit_Portfolio_UnknownFails()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Fail(s.DoAction("Edit-Portfolio PfCurrName=NoSuch PfNewName=New"));
    }

    [Fact]
    public void Delete_EmptyPortfolio_Success()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=ToDelete"));
        StalkerAssert.Ok(s.DoAction("Delete-Portfolio PfName=ToDelete"));
        Assert.Empty(s.Portfolios());
    }

    [Fact]
    public void Delete_WithHoldings_Fails()
    {
        var s = StalkerTestFixture.CreatePopulated();
        StalkerAssert.Fail(s.DoAction("Delete-Portfolio PfName=Main"), "holdings");
    }

    [Fact]
    public void Top_Portfolio_MovesToFirst()
    {
        var s = StalkerTestFixture.CreatePopulated();
        Assert.Equal("Main", s.Portfolios()[0].Name);
        StalkerAssert.Ok(s.DoAction("Top-Portfolio PfName=Dividend"));
        Assert.Equal("Dividend", s.Portfolios()[0].Name);
    }

    [Fact]
    public void Follow_AddsSRefToPortfolio()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Follow-Portfolio PfName=Test SRef=NYSE$AAPL"));
        Assert.Contains("NYSE$AAPL", s.PortfolioSRefs("Test"));
    }

    [Fact]
    public void Unfollow_RemovesSRefFromPortfolio()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Portfolio PfName=Test"));
        StalkerAssert.Ok(s.DoAction("Follow-Portfolio PfName=Test SRef=NYSE$AAPL"));
        StalkerAssert.Ok(s.DoAction("Unfollow-Portfolio PfName=Test SRef=NYSE$AAPL"));
        Assert.DoesNotContain("NYSE$AAPL", s.PortfolioSRefs("Test"));
    }
}
