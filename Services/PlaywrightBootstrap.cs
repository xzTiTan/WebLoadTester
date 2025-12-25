using System;
using System.IO;

namespace WebLoadTester.Services
{
    public static class PlaywrightBootstrap
    {
        public static string EnsureBrowsersPathAndReturn(string baseDir)
        {
            var browsersPath = Path.Combine(baseDir, "browsers");
            Directory.CreateDirectory(browsersPath);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", browsersPath);
            return browsersPath;
        }
    }
}
