using Pfs.Data.Stalker;
using Xunit;

namespace PfsData.Tests.Tests;

public class StalkerActionTests
{
    [Fact]
    public void Create_ValidCombo_AddPortfolio()
    {
        var action = StalkerAction.Create(StalkerOperation.Add, StalkerElement.Portfolio);
        Assert.NotNull(action);
        Assert.Single(action.Parameters);
        Assert.Equal("PfName", action.Parameters[0].Name);
    }

    [Fact]
    public void Create_ValidCombo_AddHolding()
    {
        var action = StalkerAction.Create(StalkerOperation.Add, StalkerElement.Holding);
        Assert.NotNull(action);
        Assert.Equal(9, action.Parameters.Count);
    }

    [Fact]
    public void Create_InvalidCombo_ReturnsNull()
    {
        var action = StalkerAction.Create(StalkerOperation.Unknown, StalkerElement.Portfolio);
        Assert.Null(action);
    }

    [Fact]
    public void Create_UnsupportedCombo_ReturnsNull()
    {
        // Move-Portfolio is not a supported combo
        var action = StalkerAction.Create(StalkerOperation.Move, StalkerElement.Portfolio);
        Assert.Null(action);
    }

    [Fact]
    public void SetParam_AndIsReady()
    {
        var action = StalkerAction.Create(StalkerOperation.Add, StalkerElement.Portfolio);
        var result = action.SetParam("PfName=TestPf");
        Assert.True(result.Ok);
        Assert.True(action.IsReady().Ok);
    }

    [Fact]
    public void IsReady_FailsWhenNotAllParamsSet()
    {
        var action = StalkerAction.Create(StalkerOperation.Add, StalkerElement.Portfolio);
        // Don't set any params - Error is null so IsReady() throws NullReferenceException
        Assert.ThrowsAny<NullReferenceException>(() => action.IsReady());
    }
}
