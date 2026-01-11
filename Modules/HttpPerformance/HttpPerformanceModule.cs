using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Core.Services;
using WebLoadTester.Infrastructure.Http;

namespace WebLoadTester.Modules.HttpPerformance;

public sealed class HttpPerformanceModule : ITestModule
{
    private readonly HttpClientProvider _provider = new();

    public string Id => "http-performance";
    public string DisplayName => "HTTP Performance";
    public TestFamily Family => TestFamily.HttpTesting;
    public Type SettingsType => typeof(HttpPerformanceSettings);

    public object CreateDefaultSettings() => new HttpPerformanceSettings();

    public IReadOnlyList<string> Validate(object settings)
    {
        if (settings is not HttpPerformanceSettings s)
        {
            return new[] { "Invalid settings" };
        }

        if (string.IsNullOrWhiteSpace(s.Url))
        {
            return new[] { "Url is required" };
        }

        if (s.TotalRequests <= 0)
        {
            return new[] { "TotalRequests must be > 0" };
        }

        if (s.Concurrency <= 0)
        {
            return new[] { "Concurrency must be > 0" };
        }

        return Array.Empty<string>();
    }

    public async Task<TestReport> RunAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (HttpPerformanceSettings)settings;
        var start = ctx.Now;
        var concurrency = Math.Min(s.Concurrency, ctx.Limits.MaxHttpConcurrency);
        var results = new ConcurrentBag<ResultItem>();
        var channel = Channel.CreateUnbounded<int>();
        for (var i = 1; i <= s.TotalRequests; i++)
        {
            await channel.Writer.WriteAsync(i, ct);
        }
        channel.Writer.Complete();

        var throttle = s.RpsLimit.HasValue ? TimeSpan.FromSeconds(1d / Math.Max(1, Math.Min(ctx.Limits.MaxRps, s.RpsLimit.Value))) : TimeSpan.Zero;
        var lastSent = DateTimeOffset.MinValue;
        var throttleLock = new object();
        var workers = Enumerable.Range(0, concurrency).Select(_ => Task.Run(async () =>
        {
            await foreach (var id in channel.Reader.ReadAllAsync(ct))
            {
                if (throttle > TimeSpan.Zero)
                {
                    TimeSpan wait;
                    lock (throttleLock)
                    {
                        wait = lastSent + throttle - DateTimeOffset.Now;
                        if (wait <= TimeSpan.Zero)
                        {
                            lastSent = DateTimeOffset.Now;
                            wait = TimeSpan.Zero;
                        }
                        else
                        {
                            lastSent = lastSent + throttle;
                        }
                    }

                    if (wait > TimeSpan.Zero)
                    {
                        await Task.Delay(wait, ct);
                    }
                }

                var sw = Stopwatch.StartNew();
                try
                {
                    var request = new HttpRequestMessage(new HttpMethod(s.Method.ToString().ToUpperInvariant()), s.Url);
                    var response = await _provider.Client.SendAsync(request, ct);
                    sw.Stop();
                    results.Add(new CheckResult
                    {
                        Kind = "Request",
                        Name = $"Request {id}",
                        Success = response.IsSuccessStatusCode,
                        DurationMs = sw.Elapsed.TotalMilliseconds,
                        StatusCode = (int)response.StatusCode,
                        ErrorMessage = response.IsSuccessStatusCode ? null : response.ReasonPhrase,
                        ErrorType = response.IsSuccessStatusCode ? null : "Http"
                    });
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    results.Add(new CheckResult
                    {
                        Kind = "Request",
                        Name = $"Request {id}",
                        Success = false,
                        DurationMs = sw.Elapsed.TotalMilliseconds,
                        ErrorMessage = ex.Message,
                        ErrorType = ex.GetType().Name
                    });
                }

                ctx.Progress.Report(new ProgressUpdate(results.Count, s.TotalRequests, $"{results.Count}/{s.TotalRequests}"));
            }
        }, ct)).ToArray();

        await Task.WhenAll(workers);

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
            Results = results.ToList()
        };
    }
}
