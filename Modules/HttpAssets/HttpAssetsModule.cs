using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Http;

namespace WebLoadTester.Modules.HttpAssets;

public sealed class HttpAssetsModule : ITestModule
{
    private readonly HttpClientProvider _clientProvider = new();

    public string Id => "http-assets";
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

    public async Task<TestReport> RunAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (HttpAssetsSettings)settings;
        var results = new List<TestResult>();
        var client = _clientProvider.Client;
        client.Timeout = TimeSpan.FromSeconds(s.TimeoutSeconds);
        var completed = 0;

        foreach (var asset in s.Assets)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using var response = await client.GetAsync(asset.Url, ct).ConfigureAwait(false);
                var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                sw.Stop();
                var success = response.IsSuccessStatusCode;
                string? error = null;
                if (success)
                {
                    if (s.ExpectedContentType is not null &&
                        response.Content.Headers.ContentType?.MediaType?.Contains(s.ExpectedContentType, StringComparison.OrdinalIgnoreCase) != true)
                    {
                        success = false;
                        error = "Content-Type mismatch";
                    }
                    if (s.MaxSizeBytes.HasValue && bytes.Length > s.MaxSizeBytes.Value)
                    {
                        success = false;
                        error = $"Size {bytes.Length} exceeds {s.MaxSizeBytes.Value}";
                    }
                    if (s.MaxLatencyMs.HasValue && sw.ElapsedMilliseconds > s.MaxLatencyMs.Value)
                    {
                        success = false;
                        error = $"Latency {sw.ElapsedMilliseconds} exceeds {s.MaxLatencyMs.Value}";
                    }
                }
                else
                {
                    error = $"HTTP {(int)response.StatusCode}";
                }

                results.Add(new CheckResult(asset.Url, success, error, sw.Elapsed.TotalMilliseconds, (int)response.StatusCode, error));
            }
            catch (Exception ex)
            {
                sw.Stop();
                results.Add(new CheckResult(asset.Url, false, ex.Message, sw.Elapsed.TotalMilliseconds, null, ex.Message));
            }
            finally
            {
                completed++;
                ctx.Progress.Report(new ProgressUpdate(completed, s.Assets.Count));
            }
        }

        return new TestReport
        {
            ModuleId = Id,
            ModuleName = DisplayName,
            Family = Family,
            StartedAt = ctx.Now,
            FinishedAt = ctx.Now,
            Status = TestStatus.Completed,
            Results = results
        };
    }
}
