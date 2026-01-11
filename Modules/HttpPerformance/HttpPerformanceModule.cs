using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Http;

namespace WebLoadTester.Modules.HttpPerformance;

public sealed class HttpPerformanceModule : ITestModule
{
    private readonly HttpClientProvider _clientProvider = new();

    public string Id => "http-performance";
    public string DisplayName => "HTTP Performance";
    public TestFamily Family => TestFamily.HttpTesting;
    public Type SettingsType => typeof(HttpPerformanceSettings);

    public object CreateDefaultSettings() => new HttpPerformanceSettings();

    public IReadOnlyList<string> Validate(object settings)
    {
        var s = (HttpPerformanceSettings)settings;
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(s.Url))
        {
            errors.Add("Url is required.");
        }
        if (s.TotalRequests <= 0)
        {
            errors.Add("TotalRequests must be positive.");
        }
        if (s.Concurrency <= 0)
        {
            errors.Add("Concurrency must be positive.");
        }
        if (s.RpsLimit.HasValue && s.RpsLimit.Value > 0 && s.RpsLimit.Value > 100)
        {
            errors.Add("RpsLimit must be <= 100.");
        }
        return errors;
    }

    public async Task<TestReport> RunAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (HttpPerformanceSettings)settings;
        var results = new List<TestResult>();
        var client = _clientProvider.Client;
        client.Timeout = TimeSpan.FromSeconds(s.TimeoutSeconds);
        var semaphore = new SemaphoreSlim(Math.Min(s.Concurrency, ctx.Limits.MaxHttpConcurrency));
        var completed = 0;

        var rateGate = new object();
        var nextAllowed = DateTimeOffset.Now;
        var interval = s.RpsLimit.HasValue && s.RpsLimit.Value > 0
            ? TimeSpan.FromSeconds(1.0 / s.RpsLimit.Value)
            : TimeSpan.Zero;

        var tasks = new List<Task>();
        for (var i = 1; i <= s.TotalRequests; i++)
        {
            await semaphore.WaitAsync(ct).ConfigureAwait(false);
            var requestId = i;
            tasks.Add(Task.Run(async () =>
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    if (interval > TimeSpan.Zero)
                    {
                        DateTimeOffset delayUntil;
                        lock (rateGate)
                        {
                            delayUntil = nextAllowed;
                            nextAllowed = nextAllowed + interval;
                        }
                        var delay = delayUntil - DateTimeOffset.Now;
                        if (delay > TimeSpan.Zero)
                        {
                            await Task.Delay(delay, ct).ConfigureAwait(false);
                        }
                    }

                    using var request = new HttpRequestMessage(new HttpMethod(s.Method), s.Url);
                    using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
                    sw.Stop();
                    var success = response.IsSuccessStatusCode;
                    var code = (int)response.StatusCode;
                    var error = success ? null : $"HTTP {code}";
                    lock (results)
                    {
                        results.Add(new RunResult($"Request {requestId}", success, error, sw.Elapsed.TotalMilliseconds, null));
                    }
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    lock (results)
                    {
                        results.Add(new RunResult($"Request {requestId}", false, ex.Message, sw.Elapsed.TotalMilliseconds, null));
                    }
                }
                finally
                {
                    var current = Interlocked.Increment(ref completed);
                    ctx.Progress.Report(new ProgressUpdate(current, s.TotalRequests));
                    semaphore.Release();
                }
            }, ct));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

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
