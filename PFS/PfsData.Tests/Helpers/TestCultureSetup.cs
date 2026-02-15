using System.Globalization;
using System.Runtime.CompilerServices;

namespace PfsData.Tests.Helpers;

internal static class TestCultureSetup
{
    [ModuleInitializer]
    internal static void SetInvariantCulture()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
    }
}
