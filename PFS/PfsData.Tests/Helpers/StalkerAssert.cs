using Pfs.Types;
using Xunit;

namespace PfsData.Tests.Helpers;

public static class StalkerAssert
{
    public static void Ok(Result result)
    {
        if (result.Fail)
        {
            string msg = result is FailResult fr ? fr.Message : "Unknown error";
            Assert.Fail($"Expected OkResult but got FailResult: {msg}");
        }
    }

    public static void Fail(Result result, string containsMessage = null)
    {
        Assert.True(result.Fail, "Expected FailResult but got OkResult");

        if (containsMessage != null && result is FailResult fr)
            Assert.Contains(containsMessage, fr.Message);
    }
}
