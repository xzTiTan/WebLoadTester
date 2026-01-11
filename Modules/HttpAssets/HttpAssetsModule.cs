using System.Diagnostics;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Http;

namespace WebLoadTester.Modules.HttpAssets;

public sealed class HttpAssetsModule : ITestModule
{
    private readonly HttpClientProvider _clientProvider = new();

    public string Id => "http.assets";
    public string DisplayName => "HTTP Assets";
    public TestFamily Family => TestFamily.HttpTesting;
    public Type SettingsType => typeof(HttpAssetsSettings);

    public object CreateDefaultSettings() => new HttpAssetsSettings();

    public IReadOnlyList<string> Validate(object settings)
    {
        var s = (HttpAssetsSettings)settings;
        var errors = new List<string>();
        if (s.Assets.Count == 0)
        {
            errors.Add("At least one asset URL is required.");
        }

        return errors;
    }

    public async Task<TestReport> RunAsync(object settings, IRunContext context, CancellationToken ct)
    {
        var s = (HttpAssetsSettings)settings;
        var report = CreateReportTemplate(context, s);
        using var client = _clientProvider.Create(TimeSpan.FromSeconds(s.TimeoutSeconds));
        var results = new List<CheckResult>();

        foreach (var asset in s.Assets)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using var response = await client.GetAsync(asset, ct);
                var bytes = await response.Content.ReadAsByteArrayAsync(ct);
                var contentType = response.Content.Headers.ContentType?.MediaType;
                var issues = new List<string>();
                if (!response.IsSuccessStatusCode)
                {
                    issues.Add($"Status {(int)response.StatusCode}");
                }
                if (s.ExpectedContentType is not null && !string.Equals(contentType, s.ExpectedContentType, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add("Content-Type mismatch");
                }
                if (s.MaxSizeBytes.HasValue && bytes.Length > s.MaxSizeBytes.Value)
                {
                    issues.Add("Size exceeds limit");
                }
                if (s.MaxLatencyMs.HasValue && sw.ElapsedMilliseconds > s.MaxLatencyMs.Value)
                {
                    issues.Add("Latency exceeds limit");
                }

                results.Add(new CheckResult
                {
                    Kind = "asset-check",
                    Name = asset,
                    Success = issues.Count == 0,
                    DurationMs = sw.ElapsedMilliseconds,
                    StatusCode = (int)response.StatusCode,
                    ErrorMessage = issues.Count == 0 ? null : string.Join("; ", issues)
                });
            }
            catch (Exception ex)
            {
                results.Add(new CheckResult
                {
                    Kind = "asset-check",
                    Name = asset,
                    Success = false,
                    DurationMs = sw.ElapsedMilliseconds,
                    ErrorMessage = ex.Message,
                    ErrorType = ex.GetType().Name
                });
            }

            context.Progress.Report(results.Count, s.Assets.Count);
        }

        report.Results.AddRange(results);
        report = report with { Metrics = Core.Services.Metrics.MetricsCalculator.Calculate(report.Results) };
        report = report with { Meta = report.Meta with { Status = TestStatus.Completed, FinishedAt = context.Now } };
        return report;
    }

    private static TestReport CreateReportTemplate(IRunContext context, HttpAssetsSettings settings)
    {
        return new TestReport
        {
            Meta = new ReportMeta
            {
                Status = TestStatus.Completed,
                StartedAt = context.Now,
                FinishedAt = context.Now,
                AppVersion = typeof(HttpAssetsModule).Assembly.GetName().Version?.ToString() ?? "unknown",
                OperatingSystem = Environment.OSVersion.ToString()
            },
            SettingsSnapshot = System.Text.Json.JsonSerializer.SerializeToElement(settings)
        };
    }
}
