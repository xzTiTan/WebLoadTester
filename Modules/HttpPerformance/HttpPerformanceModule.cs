using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Http;

namespace WebLoadTester.Modules.HttpPerformance;

public class HttpPerformanceModule : ITestModule
{
    public string Id => "http.performance";
    public string DisplayName => "HTTP Performance";
    public TestFamily Family => TestFamily.HttpTesting;
    public Type SettingsType => typeof(HttpPerformanceSettings);

    public object CreateDefaultSettings()
    {
        return new HttpPerformanceSettings();
    }

    public IReadOnlyList<string> Validate(object settings)
    {
        var errors = new List<string>();
        if (settings is not HttpPerformanceSettings s)
        {
            errors.Add("Invalid settings type");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(s.Url))
        {
            errors.Add("Url is required");
        }

        if (s.TotalRequests <= 0)
        {
            errors.Add("TotalRequests must be positive");
        }

        if (s.Concurrency <= 0)
        {
            errors.Add("Concurrency must be positive");
        }

        return errors;
    }

    public async Task<TestReport> RunAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (HttpPerformanceSettings)settings;
        var report = new TestReport
        {
            ModuleId = Id,
            ModuleName = DisplayName,
            Family = Family,
            StartedAt = ctx.Now,
            Status = TestStatus.Completed,
            SettingsSnapshot = System.Text.Json.JsonSerializer.Serialize(s),
            AppVersion = typeof(HttpPerformanceModule).Assembly.GetName().Version?.ToString() ?? string.Empty,
            OsDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription
        };

        using var client = HttpClientProvider.Create(TimeSpan.FromSeconds(s.TimeoutSeconds));
        var results = new ConcurrentBag<ResultBase>();
        var semaphore = new SemaphoreSlim(Math.Min(s.Concurrency, ctx.Limits.MaxHttpConcurrency));
        var throttle = new SemaphoreSlim(1, 1);
        var completed = 0;

        var tasks = new List<Task>();
        for (var i = 0; i < s.TotalRequests; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    if (s.RpsLimit.HasValue)
                    {
                        await throttle.WaitAsync(ct);
                        _ = Task.Delay(TimeSpan.FromSeconds(1.0 / Math.Min(s.RpsLimit.Value, ctx.Limits.MaxRps)), ct)
                            .ContinueWith(_ => throttle.Release());
                    }

                    var sw = Stopwatch.StartNew();
                    try
                    {
                        var response = await client.SendAsync(new HttpRequestMessage(s.Method, s.Url), ct);
                        sw.Stop();
                        results.Add(new CheckResult("Request")
                        {
                            Success = response.IsSuccessStatusCode,
                            DurationMs = sw.Elapsed.TotalMilliseconds,
                            StatusCode = (int)response.StatusCode,
                            ErrorType = response.IsSuccessStatusCode ? null : "Http",
                            ErrorMessage = response.IsSuccessStatusCode ? null : response.StatusCode.ToString()
                        });
                    }
                    catch (Exception ex)
                    {
                        sw.Stop();
                        results.Add(new CheckResult("Request")
                        {
                            Success = false,
                            DurationMs = sw.Elapsed.TotalMilliseconds,
                            ErrorType = ex.GetType().Name,
                            ErrorMessage = ex.Message
                        });
                    }
                    finally
                    {
                        var done = Interlocked.Increment(ref completed);
                        ctx.Progress.Report(new ProgressUpdate(done, s.TotalRequests, "HTTP Performance"));
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }, ct));
        }

        await Task.WhenAll(tasks);
        report.Results = new List<ResultBase>(results);
        report.FinishedAt = ctx.Now;
        return report;
    }
}
