using Pfs.Data.Stalker;
using Xunit;

namespace PfsData.Tests.Tests;

public class StalkerSplitTests
{
    [Fact]
    public void SplitLine_SimpleWords()
    {
        var result = StalkerSplit.SplitLine("Add-Portfolio PfName=Test");
        Assert.Equal(2, result.Count);
        Assert.Equal("Add-Portfolio", result[0]);
        Assert.Equal("PfName=Test", result[1]);
    }

    [Fact]
    public void SplitLine_BracketedString()
    {
        var result = StalkerSplit.SplitLine("Note=String:0:100 Note=[This is a note]");
        Assert.Equal(2, result.Count);
        Assert.Equal("Note=This is a note", result[1]);
    }

    [Fact]
    public void SplitLine_StandaloneBracketedString()
    {
        var result = StalkerSplit.SplitLine("cmd [hello world]");
        Assert.Equal(2, result.Count);
        Assert.Equal("hello world", result[1]);
    }

    [Fact]
    public void SplitLine_MultipleBracketedStrings()
    {
        var result = StalkerSplit.SplitLine("A [hello world] B=[foo bar]");
        Assert.Equal(3, result.Count);
        Assert.Equal("hello world", result[1]);
        Assert.Equal("B=foo bar", result[2]);
    }

    [Fact]
    public void SplitLine_EmptyString()
    {
        var result = StalkerSplit.SplitLine("");
        Assert.Empty(result);
    }

    [Fact]
    public void SplitLine_MultipleSpaces()
    {
        var result = StalkerSplit.SplitLine("A   B   C");
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void SplitLine_BracketNotAfterSpaceOrEquals()
    {
        // '[' that isn't after ' ' or '=' should be treated as literal
        var result = StalkerSplit.SplitLine("A x[y]z B");
        Assert.Equal(3, result.Count);
        Assert.Equal("x[y]z", result[1]);
    }

    [Fact]
    public void SplitLine_NestedBrackets()
    {
        var result = StalkerSplit.SplitLine("cmd [outer [inner] text]");
        Assert.Equal(2, result.Count);
        Assert.Equal("outer inner text", result[1]);
    }
}
