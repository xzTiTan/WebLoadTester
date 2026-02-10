using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Http;

namespace WebLoadTester.Modules.SecurityBaseline;

/// <summary>
/// Модуль проверки базовых security-практик (заголовки, редирект, TLS).
/// </summary>
public class SecurityBaselineModule : ITestModule
{
    public string Id => "net.security";
    public string DisplayName => "Базовая безопасность";
    public string Description => "Проверяет базовые security-настройки без атакующих действий.";
    public TestFamily Family => TestFamily.NetSec;
    public Type SettingsType => typeof(SecurityBaselineSettings);

    /// <summary>
    /// Создаёт настройки по умолчанию.
    /// </summary>
    public object CreateDefaultSettings() => new SecurityBaselineSettings();

    /// <summary>
    /// Проверяет корректность настроек.
    /// </summary>
    public IReadOnlyList<string> Validate(object settings)
    {
        var errors = new List<string>();
        if (settings is not SecurityBaselineSettings s)
        {
            errors.Add("Invalid settings type");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(s.Url))
        {
            errors.Add("Url required");
        }

        return errors;
    }

    /// <summary>
    /// Выполняет проверки безопасности и формирует результат.
    /// </summary>
    public async Task<ModuleResult> ExecuteAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (SecurityBaselineSettings)settings;
        var result = new ModuleResult();
        var totalChecks = (s.CheckHsts ? 1 : 0) + (s.CheckContentTypeOptions ? 1 : 0) + (s.CheckFrameOptions ? 1 : 0)
            + (s.CheckContentSecurityPolicy ? 1 : 0) + (s.CheckReferrerPolicy ? 1 : 0) + (s.CheckPermissionsPolicy ? 1 : 0)
            + (s.CheckRedirectHttpToHttps ? 1 : 0);
        var current = 0;
        ctx.Progress.Report(new ProgressUpdate(0, Math.Max(totalChecks, 1), "Security baseline старт"));
        ctx.Log.Info($"[SecurityBaseline] Target={s.Url}");

        using var client = HttpClientProvider.Create(TimeSpan.FromSeconds(10));
        var results = new List<ResultBase>();
        HttpResponseMessage response;
        var requestSw = Stopwatch.StartNew();
        try
        {
            response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, s.Url), ct);
            requestSw.Stop();
        }
        catch (Exception ex)
        {
            requestSw.Stop();
            results.Add(new CheckResult("HTTP request")
            {
                Success = false,
                DurationMs = requestSw.Elapsed.TotalMilliseconds,
                ErrorType = ex.GetType().Name,
                ErrorMessage = ex.Message
            });

            result.Results = results;
            result.Status = TestStatus.Failed;
            return result;
        }
        var headers = response.Headers;

        if (s.CheckHsts && s.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            AddHeaderCheck(results, "Strict-Transport-Security", headers.Contains("Strict-Transport-Security"));
            current++;
            ctx.Progress.Report(new ProgressUpdate(current, Math.Max(totalChecks, 1), "HSTS"));
        }

        if (s.CheckContentTypeOptions)
        {
            var value = headers.TryGetValues("X-Content-Type-Options", out var values)
                ? string.Join(";", values)
                : string.Empty;
            var ok = value.Contains("nosniff", StringComparison.OrdinalIgnoreCase);
            AddHeaderCheck(results, "X-Content-Type-Options", ok, ok ? null : "Expected nosniff");
            current++;
            ctx.Progress.Report(new ProgressUpdate(current, Math.Max(totalChecks, 1), "X-Content-Type-Options"));
        }

        if (s.CheckFrameOptions)
        {
            var value = headers.TryGetValues("X-Frame-Options", out var values)
                ? string.Join(";", values)
                : string.Empty;
            var ok = value.Contains("DENY", StringComparison.OrdinalIgnoreCase) ||
                     value.Contains("SAMEORIGIN", StringComparison.OrdinalIgnoreCase);
            AddHeaderCheck(results, "X-Frame-Options", ok, ok ? null : "Expected DENY or SAMEORIGIN");
            current++;
            ctx.Progress.Report(new ProgressUpdate(current, Math.Max(totalChecks, 1), "X-Frame-Options"));
        }

        if (s.CheckContentSecurityPolicy)
        {
            AddHeaderCheck(results, "Content-Security-Policy", headers.Contains("Content-Security-Policy"));
            current++;
            ctx.Progress.Report(new ProgressUpdate(current, Math.Max(totalChecks, 1), "Content-Security-Policy"));
        }

        if (s.CheckReferrerPolicy)
        {
            AddHeaderCheck(results, "Referrer-Policy", headers.Contains("Referrer-Policy"));
            current++;
            ctx.Progress.Report(new ProgressUpdate(current, Math.Max(totalChecks, 1), "Referrer-Policy"));
        }

        if (s.CheckPermissionsPolicy)
        {
            AddHeaderCheck(results, "Permissions-Policy", headers.Contains("Permissions-Policy"));
            current++;
            ctx.Progress.Report(new ProgressUpdate(current, Math.Max(totalChecks, 1), "Permissions-Policy"));
        }

        if (s.CheckRedirectHttpToHttps && s.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var redirectClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
            var httpResponse = await redirectClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, s.Url), ct);
            var redirect = (int)httpResponse.StatusCode >= 300 && (int)httpResponse.StatusCode < 400;
            var location = httpResponse.Headers.Location?.ToString() ?? string.Empty;
            var httpsRedirect = redirect && location.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            AddHeaderCheck(results, "HTTP->HTTPS", httpsRedirect, httpsRedirect ? null : "No HTTPS redirect");
            current++;
            ctx.Progress.Report(new ProgressUpdate(current, Math.Max(totalChecks, 1), "HTTP->HTTPS"));
        }

        result.Results = results;
        result.Status = results.Any(r => !r.Success) ? TestStatus.Failed : TestStatus.Success;
        return result;
    }

    private static void AddHeaderCheck(ICollection<ResultBase> results, string name, bool success, string? message = null)
    {
        results.Add(new CheckResult(name)
        {
            Success = success,
            DurationMs = 0,
            ErrorType = success ? null : "Warn",
            ErrorMessage = success ? null : message ?? "Missing"
        });
    }
}
