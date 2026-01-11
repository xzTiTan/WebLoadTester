using System.Reflection;

namespace WebLoadTester.Core.Services;

public static class AppVersionProvider
{
    public static string GetVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
    }
}
