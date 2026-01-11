using System.Diagnostics;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Core.Services;
using WebLoadTester.Infrastructure.Http;

namespace WebLoadTester.Modules.HttpAssets;

public sealed class HttpAssetsModule : ITestModule
{
    private readonly HttpClientProvider _provider = new();

    public string Id => "http-assets";
    public string DisplayName => "HTTP Assets";
    public TestFamily Family => TestFamily.HttpTesting;
    public Type SettingsType => typeof(HttpAssetsSettings);

    public object CreateDefaultSettings() => new HttpAssetsSettings();

    public IReadOnlyList<string> Validate(object settings)
    {
        if (settings is not HttpAssetsSettings s)
        {
            return new[] { "Invalid settings" };
        }

        if (s.Assets.Count == 0)
        {
            return new[] { "Assets list is empty" };
        }

        return Array.Empty<string>();
    }

    public async Task<TestReport> RunAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (HttpAssetsSettings)settings;
        var start = ctx.Now;
        var results = new List<ResultItem>();

        foreach (var asset in s.Assets)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var response = await _provider.Client.GetAsync(asset.Url, ct);
                var bytes = await response.Content.ReadAsByteArrayAsync(ct);
                sw.Stop();

                var errors = new List<string>();
                if (s.ExpectedContentType is not null && response.Content.Headers.ContentType?.MediaType != s.ExpectedContentType)
                {
                    errors.Add("ContentType mismatch");
                }

                if (s.MaxSizeBytes.HasValue && bytes.Length > s.MaxSizeBytes.Value)
                {
                    errors.Add("Size exceeded");
                }

                if (s.MaxLatencyMs.HasValue && sw.Elapsed.TotalMilliseconds > s.MaxLatencyMs.Value)
                {
                    errors.Add("Latency exceeded");
                }

                results.Add(new CheckResult
                {
                    Kind = "Asset",
                    Name = asset.Url,
                    Success = errors.Count == 0,
                    DurationMs = sw.Elapsed.TotalMilliseconds,
                    StatusCode = (int)response.StatusCode,
                    ErrorMessage = errors.Count == 0 ? null : string.Join("; ", errors),
                    ErrorType = errors.Count == 0 ? null : "Asset"
                });
            }
            catch (Exception ex)
            {
                sw.Stop();
                results.Add(new CheckResult
                {
                    Kind = "Asset",
                    Name = asset.Url,
                    Success = false,
                    DurationMs = sw.Elapsed.TotalMilliseconds,
                    ErrorMessage = ex.Message,
                    ErrorType = ex.GetType().Name
                });
            }

            ctx.Progress.Report(new ProgressUpdate(results.Count, s.Assets.Count, asset.Url));
        }

        return new TestReport
        {
            ModuleId = Id,
            ModuleName = DisplayName,
            Family = Family,
            StartedAt = start,
            FinishedAt = ctx.Now,
            Status = TestStatus.Completed,
            AppVersion = AppVersionProvider.GetVersion(),
            OsDescription = Environment.OSVersion.ToString(),
            SettingsSnapshot = System.Text.Json.JsonSerializer.SerializeToElement(settings),
            Results = results
        };
    }
}
