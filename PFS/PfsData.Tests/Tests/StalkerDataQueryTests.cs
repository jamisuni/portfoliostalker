using Pfs.Data.Stalker;
using Pfs.Types;
using PfsData.Tests.Helpers;
using Xunit;

namespace PfsData.Tests.Tests;

public class StalkerDataQueryTests
{
    [Fact]
    public void Portfolios_ReturnsAll()
    {
        var s = StalkerTestFixture.CreatePopulated();
        Assert.Equal(3, s.Portfolios().Count);
    }

    [Fact]
    public void PortfolioSRefs_ReturnsSorted()
    {
        var s = StalkerTestFixture.CreatePopulated();
        var sRefs = s.PortfolioSRefs("Main");
        Assert.True(sRefs.Count >= 3);
        for (int i = 1; i < sRefs.Count; i++)
            Assert.True(string.Compare(sRefs[i - 1], sRefs[i]) <= 0);
    }

    [Fact]
    public void PortfolioHoldings_FilterBySRef()
    {
        var s = StalkerTestFixture.CreatePopulated();
        var msftHoldings = s.PortfolioHoldings("Main", "NASDAQ$MSFT");
        Assert.Equal(2, msftHoldings.Count);
        Assert.All(msftHoldings, h => Assert.Equal("NASDAQ$MSFT", h.SRef));
    }

    [Fact]
    public void PortfolioHoldings_UnknownPf_ReturnsEmpty()
    {
        var s = StalkerTestFixture.CreatePopulated();
        var holdings = s.PortfolioHoldings("NonExistent");
        Assert.Empty(holdings);
    }

    [Fact]
    public void IsPurhaceId_FindsExisting()
    {
        var s = StalkerTestFixture.CreatePopulated();
        Assert.True(s.IsPurhaceId("M001"));
        Assert.False(s.IsPurhaceId("NONEXISTENT"));
    }

    [Fact]
    public void StockRef_FindsKnown()
    {
        var s = StalkerTestFixture.CreatePopulated();
        Assert.NotNull(s.StockRef("NASDAQ$MSFT"));
        Assert.Null(s.StockRef("NYSE$FAKE"));
    }

    [Fact]
    public void DeepCopy_ProducesIndependentCopy()
    {
        var original = StalkerTestFixture.CreatePopulated();
        var copy = new StalkerDoCmd();
        copy.Init();
        StalkerDoCmd.DeepCopy(original, copy);

        // Verify same data
        Assert.Equal(original.Portfolios().Count, copy.Portfolios().Count);
        Assert.Equal(original.Stocks().Count, copy.Stocks().Count);

        // Modify original, verify copy not affected
        original.DoAction("Add-Portfolio PfName=Extra");
        Assert.Equal(4, original.Portfolios().Count);
        Assert.Equal(3, copy.Portfolios().Count);
    }

    [Fact]
    public void GetActions_TracksCommands()
    {
        var s = StalkerTestFixture.CreateEmpty();
        s.TrackActions();
        s.DoAction("Add-Portfolio PfName=Test");
        s.DoAction("Add-Portfolio PfName=Second");
        var actions = s.GetActions();
        Assert.Equal(2, actions.Count);
        Assert.Contains("Add-Portfolio PfName=Test", actions);
    }
}
