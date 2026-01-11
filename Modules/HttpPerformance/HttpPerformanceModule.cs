using System.Diagnostics;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Http;

namespace WebLoadTester.Modules.HttpPerformance;

public sealed class HttpPerformanceModule : ITestModule
{
    private readonly HttpClientProvider _clientProvider = new();

    public string Id => "http.performance";
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
            errors.Add("URL is required.");
        }

        if (s.Concurrency is < 1 or > 50)
        {
            errors.Add("Concurrency must be between 1 and 50.");
        }

        if (s.TotalRequests < 1)
        {
            errors.Add("Total requests must be >= 1.");
        }

        if (s.RpsLimit is < 1 or > 100)
        {
            errors.Add("RPS limit must be between 1 and 100.");
        }

        return errors;
    }

    public async Task<TestReport> RunAsync(object settings, IRunContext context, CancellationToken ct)
    {
        var s = (HttpPerformanceSettings)settings;
        var report = CreateReportTemplate(context, s);
        var results = new List<RunResult>();
        using var client = _clientProvider.Create(TimeSpan.FromSeconds(s.TimeoutSeconds));
        using var semaphore = new SemaphoreSlim(s.Concurrency);
        var requestCounter = 0;
        var limiter = new RateLimiter(s.RpsLimit);

        var tasks = Enumerable.Range(0, s.TotalRequests).Select(async i =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                await limiter.WaitAsync(ct);
                var sw = Stopwatch.StartNew();
                try
                {
                    using var request = new HttpRequestMessage(new HttpMethod(s.Method), s.Url);
                    using var response = await client.SendAsync(request, ct);
                    lock (results)
                    {
                        results.Add(new RunResult
                        {
                            Kind = "http-load",
                            Name = $"Request {Interlocked.Increment(ref requestCounter)}",
                            Success = response.IsSuccessStatusCode,
                            DurationMs = sw.ElapsedMilliseconds,
                            StatusCode = (int)response.StatusCode,
                            Url = s.Url
                        });
                        context.Progress.Report(results.Count, s.TotalRequests);
                    }
                }
                catch (Exception ex)
                {
                    lock (results)
                    {
                        results.Add(new RunResult
                        {
                            Kind = "http-load",
                            Name = $"Request {Interlocked.Increment(ref requestCounter)}",
                            Success = false,
                            DurationMs = sw.ElapsedMilliseconds,
                            Url = s.Url,
                            ErrorMessage = ex.Message,
                            ErrorType = ex.GetType().Name
                        });
                    }
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        report.Results.AddRange(results);
        report = report with { Metrics = Core.Services.Metrics.MetricsCalculator.Calculate(report.Results) };
        report = report with { Meta = report.Meta with { Status = TestStatus.Completed, FinishedAt = context.Now } };
        return report;
    }

    private static TestReport CreateReportTemplate(IRunContext context, HttpPerformanceSettings settings)
    {
        return new TestReport
        {
            Meta = new ReportMeta
            {
                Status = TestStatus.Completed,
                StartedAt = context.Now,
                FinishedAt = context.Now,
                AppVersion = typeof(HttpPerformanceModule).Assembly.GetName().Version?.ToString() ?? "unknown",
                OperatingSystem = Environment.OSVersion.ToString()
            },
            SettingsSnapshot = System.Text.Json.JsonSerializer.SerializeToElement(settings)
        };
    }

    private sealed class RateLimiter
    {
        private readonly int? _limit;
        private DateTime _windowStart = DateTime.UtcNow;
        private int _count;

        public RateLimiter(int? limit)
        {
            _limit = limit;
        }

        public Task WaitAsync(CancellationToken ct)
        {
            if (_limit is null)
            {
                return Task.CompletedTask;
            }

            var now = DateTime.UtcNow;
            if ((now - _windowStart).TotalSeconds >= 1)
            {
                _windowStart = now;
                Interlocked.Exchange(ref _count, 0);
            }

            if (Interlocked.Increment(ref _count) > _limit.Value)
            {
                return Task.Delay(1000 - (now - _windowStart).Milliseconds, ct);
            }

            return Task.CompletedTask;
        }
    }
}
