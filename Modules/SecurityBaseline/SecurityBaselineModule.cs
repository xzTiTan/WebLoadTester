using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Http;

namespace WebLoadTester.Modules.SecurityBaseline;

/// <summary>
/// Модуль проверки базовых security-практик.
/// </summary>
public class SecurityBaselineModule : ITestModule
{
    public string Id => "net.security";
    public string DisplayName => "Базовая безопасность";
    public string Description => "Проверяет базовые security-настройки без атакующих действий.";
    public TestFamily Family => TestFamily.NetSec;
    public Type SettingsType => typeof(SecurityBaselineSettings);

    public object CreateDefaultSettings() => new SecurityBaselineSettings();

    public IReadOnlyList<string> Validate(object settings)
    {
        var errors = new List<string>();
        if (settings is not SecurityBaselineSettings s)
        {
            errors.Add("Некорректный тип настроек net.security.");
            return errors;
        }

        if (!Uri.TryCreate(s.Url, UriKind.Absolute, out _))
        {
            errors.Add("Url обязателен и должен быть абсолютным URL.");
        }

        if (!s.CheckHsts && !s.CheckContentTypeOptions && !s.CheckFrameOptions && !s.CheckContentSecurityPolicy &&
            !s.CheckReferrerPolicy && !s.CheckPermissionsPolicy && !s.CheckRedirectHttpToHttps && !s.CheckCookieFlags)
        {
            errors.Add("Включите хотя бы одну проверку security baseline.");
        }

        return errors;
    }

    public async Task<ModuleResult> ExecuteAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (SecurityBaselineSettings)settings;
        var results = new List<ResultBase>();
        var checks = GetChecksCount(s);
        var current = 0;
        ctx.Progress.Report(new ProgressUpdate(0, Math.Max(checks, 1), "Security baseline"));

        using var client = HttpClientProvider.Create(TimeSpan.FromSeconds(10));
        HttpResponseMessage? response = null;
        var requestSw = Stopwatch.StartNew();
        try
        {
            response = await client.GetAsync(s.Url, ct);
            requestSw.Stop();
        }
        catch (TaskCanceledException ex)
        {
            requestSw.Stop();
            AddGlobalFail(results, s, "Timeout", $"Не удалось получить ответ: {ex.Message}");
            return BuildResult(results);
        }
        catch (Exception ex)
        {
            requestSw.Stop();
            AddGlobalFail(results, s, "Exception", $"Не удалось получить ответ: {ex.Message}");
            return BuildResult(results);
        }

        var headers = response.Headers;

        if (s.CheckHsts)
        {
            var enabled = s.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            var present = headers.Contains("Strict-Transport-Security");
            results.Add(CreateBaselineResult(
                "HSTS",
                enabled ? (present ? "Pass" : "Warn") : "NA",
                enabled
                    ? (present
                        ? "HSTS включен: браузер будет принудительно использовать HTTPS."
                        : "HSTS отсутствует: пользователь может быть уязвим к downgrade-атакам при первом обращении.")
                    : "Проверка HSTS неприменима для HTTP-цели.",
                present,
                JsonSerializer.SerializeToElement(new { header = "Strict-Transport-Security", present })));
            current++;
            ctx.Progress.Report(new ProgressUpdate(current, Math.Max(checks, 1), "HSTS"));
        }

        if (s.CheckContentTypeOptions)
        {
            var value = TryGetHeader(headers, "X-Content-Type-Options");
            var ok = value.Contains("nosniff", StringComparison.OrdinalIgnoreCase);
            results.Add(CreateBaselineResult(
                "X-Content-Type-Options",
                ok ? "Pass" : "Warn",
                ok
                    ? "X-Content-Type-Options=nosniff защищает от MIME-sniffing атак."
                    : "Отсутствует nosniff: браузер может интерпретировать контент небезопасно.",
                ok,
                JsonSerializer.SerializeToElement(new { header = "X-Content-Type-Options", value })));
            current++;
            ctx.Progress.Report(new ProgressUpdate(current, Math.Max(checks, 1), "X-Content-Type-Options"));
        }

        if (s.CheckFrameOptions)
        {
            var value = TryGetHeader(headers, "X-Frame-Options");
            var ok = value.Contains("DENY", StringComparison.OrdinalIgnoreCase) || value.Contains("SAMEORIGIN", StringComparison.OrdinalIgnoreCase);
            results.Add(CreateBaselineResult(
                "Frame protection",
                ok ? "Pass" : "Warn",
                ok
                    ? "Защита от встраивания включена (X-Frame-Options)."
                    : "Нет защиты от clickjacking: ресурс можно встроить во фрейм.",
                ok,
                JsonSerializer.SerializeToElement(new { header = "X-Frame-Options", value })));
            current++;
            ctx.Progress.Report(new ProgressUpdate(current, Math.Max(checks, 1), "Frame protection"));
        }

        if (s.CheckContentSecurityPolicy)
        {
            var value = TryGetHeader(headers, "Content-Security-Policy");
            var ok = !string.IsNullOrWhiteSpace(value);
            results.Add(CreateBaselineResult(
                "Content-Security-Policy",
                ok ? "Pass" : "Warn",
                ok
                    ? "CSP задан: снижается риск XSS и загрузки нежелательных источников."
                    : "CSP отсутствует: браузер не ограничивает источники скриптов и контента.",
                ok,
                JsonSerializer.SerializeToElement(new { header = "Content-Security-Policy", value })));
            current++;
            ctx.Progress.Report(new ProgressUpdate(current, Math.Max(checks, 1), "CSP"));
        }

        if (s.CheckReferrerPolicy)
        {
            var value = TryGetHeader(headers, "Referrer-Policy");
            var ok = !string.IsNullOrWhiteSpace(value);
            results.Add(CreateBaselineResult(
                "Referrer-Policy",
                ok ? "Pass" : "Warn",
                ok
                    ? "Referrer-Policy задан: контролируется утечка URL в Referer."
                    : "Referrer-Policy отсутствует: возможна избыточная передача данных о переходах.",
                ok,
                JsonSerializer.SerializeToElement(new { header = "Referrer-Policy", value })));
            current++;
            ctx.Progress.Report(new ProgressUpdate(current, Math.Max(checks, 1), "Referrer-Policy"));
        }

        if (s.CheckPermissionsPolicy)
        {
            var value = TryGetHeader(headers, "Permissions-Policy");
            var ok = !string.IsNullOrWhiteSpace(value);
            results.Add(CreateBaselineResult(
                "Permissions-Policy",
                ok ? "Pass" : "Warn",
                ok
                    ? "Permissions-Policy ограничивает потенциально опасные браузерные API."
                    : "Permissions-Policy отсутствует: браузерные API не ограничены политикой сервера.",
                ok,
                JsonSerializer.SerializeToElement(new { header = "Permissions-Policy", value })));
            current++;
            ctx.Progress.Report(new ProgressUpdate(current, Math.Max(checks, 1), "Permissions-Policy"));
        }

        if (s.CheckRedirectHttpToHttps)
        {
            if (s.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                using var noRedirectClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
                {
                    Timeout = TimeSpan.FromSeconds(10)
                };
                var redirectResponse = await noRedirectClient.GetAsync(s.Url, ct);
                var location = redirectResponse.Headers.Location?.ToString() ?? string.Empty;
                var ok = (int)redirectResponse.StatusCode is >= 300 and < 400 && location.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
                results.Add(CreateBaselineResult(
                    "HTTP→HTTPS redirect",
                    ok ? "Pass" : "Warn",
                    ok
                        ? "HTTP-трафик перенаправляется на HTTPS."
                        : "HTTP не перенаправляется на HTTPS: возможен небезопасный доступ без шифрования.",
                    ok,
                    JsonSerializer.SerializeToElement(new { statusCode = (int)redirectResponse.StatusCode, location })));
            }
            else
            {
                results.Add(CreateBaselineResult(
                    "HTTP→HTTPS redirect",
                    "NA",
                    "Проверка редиректа неприменима: указан HTTPS URL.",
                    true,
                    JsonSerializer.SerializeToElement(new { skipped = true })));
            }

            current++;
            ctx.Progress.Report(new ProgressUpdate(current, Math.Max(checks, 1), "Redirect"));
        }

        if (s.CheckCookieFlags)
        {
            var cookies = response.Headers.TryGetValues("Set-Cookie", out var values)
                ? values.ToArray()
                : Array.Empty<string>();
            if (cookies.Length == 0)
            {
                results.Add(CreateBaselineResult(
                    "Cookie flags",
                    "NA",
                    "Set-Cookie отсутствует: проверка cookie-флагов неприменима.",
                    true,
                    JsonSerializer.SerializeToElement(new { cookieCount = 0 })));
            }
            else
            {
                var weakCookies = cookies.Where(c => !c.Contains("Secure", StringComparison.OrdinalIgnoreCase) || !c.Contains("HttpOnly", StringComparison.OrdinalIgnoreCase)).ToArray();
                var ok = weakCookies.Length == 0;
                results.Add(CreateBaselineResult(
                    "Cookie flags",
                    ok ? "Pass" : "Warn",
                    ok
                        ? "Cookie-флаги Secure и HttpOnly заданы для всех cookies."
                        : "Часть cookies без Secure/HttpOnly: это повышает риск кражи cookie через сеть или XSS.",
                    ok,
                    JsonSerializer.SerializeToElement(new { cookieCount = cookies.Length, weakCookies })));
            }

            current++;
            ctx.Progress.Report(new ProgressUpdate(current, Math.Max(checks, 1), "Cookie flags"));
        }

        return BuildResult(results);
    }

    private static int GetChecksCount(SecurityBaselineSettings s)
    {
        return (s.CheckHsts ? 1 : 0) + (s.CheckContentTypeOptions ? 1 : 0) + (s.CheckFrameOptions ? 1 : 0) +
               (s.CheckContentSecurityPolicy ? 1 : 0) + (s.CheckReferrerPolicy ? 1 : 0) + (s.CheckPermissionsPolicy ? 1 : 0) +
               (s.CheckRedirectHttpToHttps ? 1 : 0) + (s.CheckCookieFlags ? 1 : 0);
    }

    private static string TryGetHeader(System.Net.Http.Headers.HttpResponseHeaders headers, string key)
    {
        return headers.TryGetValues(key, out var values)
            ? string.Join(';', values)
            : string.Empty;
    }

    private static void AddGlobalFail(List<ResultBase> results, SecurityBaselineSettings settings, string errorKind, string message)
    {
        foreach (var checkName in EnumerateEnabledChecks(settings))
        {
            results.Add(new CheckResult(checkName)
            {
                Success = false,
                ErrorType = errorKind,
                ErrorMessage = message,
                Severity = "Fail",
                Metrics = JsonSerializer.SerializeToElement(new { reason = message })
            });
        }
    }

    private static IEnumerable<string> EnumerateEnabledChecks(SecurityBaselineSettings s)
    {
        if (s.CheckHsts) yield return "HSTS";
        if (s.CheckContentTypeOptions) yield return "X-Content-Type-Options";
        if (s.CheckFrameOptions) yield return "Frame protection";
        if (s.CheckContentSecurityPolicy) yield return "Content-Security-Policy";
        if (s.CheckReferrerPolicy) yield return "Referrer-Policy";
        if (s.CheckPermissionsPolicy) yield return "Permissions-Policy";
        if (s.CheckRedirectHttpToHttps) yield return "HTTP→HTTPS redirect";
        if (s.CheckCookieFlags) yield return "Cookie flags";
    }

    private static CheckResult CreateBaselineResult(string name, string severity, string message, bool ok, JsonElement metrics)
    {
        return new CheckResult(name)
        {
            Success = severity != "Fail",
            DurationMs = 0,
            ErrorType = ok ? null : "Baseline",
            ErrorMessage = message,
            Severity = severity,
            Metrics = metrics
        };
    }

    private static ModuleResult BuildResult(List<ResultBase> results)
    {
        var hasFail = results.OfType<CheckResult>().Any(r => string.Equals(r.Severity, "Fail", StringComparison.OrdinalIgnoreCase));
        return new ModuleResult
        {
            Results = results,
            Status = hasFail ? TestStatus.Failed : TestStatus.Success
        };
    }
}
