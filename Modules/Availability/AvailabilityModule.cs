using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Http;

namespace WebLoadTester.Modules.Availability;

/// <summary>
/// Модуль проверки доступности HTTP/TCP.
/// </summary>
public class AvailabilityModule : ITestModule
{
    public string Id => "net.availability";
    public string DisplayName => "Доступность";
    public string Description => "Проверяет доступность HTTP/TCP цели. Одна итерация = одна проверка.";
    public TestFamily Family => TestFamily.NetSec;
    public Type SettingsType => typeof(AvailabilitySettings);

    public object CreateDefaultSettings() => new AvailabilitySettings();

    public IReadOnlyList<string> Validate(object settings)
    {
        var errors = new List<string>();
        if (settings is not AvailabilitySettings s)
        {
            errors.Add("Некорректный тип настроек net.availability.");
            return errors;
        }

        s.NormalizeLegacy();

        if (s.TimeoutMs <= 0)
        {
            errors.Add("TimeoutMs должен быть больше 0.");
        }

        if (s.CheckType.Equals("HTTP", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(s.Url, UriKind.Absolute, out _))
            {
                errors.Add("Для HTTP-проверки поле Url обязательно и должно быть полным URL.");
            }
        }
        else if (s.CheckType.Equals("TCP", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(s.Host))
            {
                errors.Add("Для TCP-проверки поле Host обязательно.");
            }

            if (s.Port is < 1 or > 65535)
            {
                errors.Add("Для TCP-проверки поле Port должно быть в диапазоне 1..65535.");
            }
        }
        else
        {
            errors.Add("CheckType должен быть HTTP или TCP.");
        }

        return errors;
    }

    public async Task<ModuleResult> ExecuteAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (AvailabilitySettings)settings;
        s.NormalizeLegacy();

        var sw = Stopwatch.StartNew();
        var ok = false;
        var errorKind = (string?)null;
        var message = "Проверка завершена успешно.";
        int? statusCode = null;

        try
        {
            if (s.CheckType.Equals("TCP", StringComparison.OrdinalIgnoreCase))
            {
                using var client = new TcpClient();
                await client.ConnectAsync(s.Host, s.Port, ct);
                ok = true;
            }
            else
            {
                using var client = HttpClientProvider.Create(TimeSpan.FromMilliseconds(s.TimeoutMs));
                using var response = await client.GetAsync(s.Url, ct);
                statusCode = (int)response.StatusCode;
                ok = response.IsSuccessStatusCode;
                if (!ok)
                {
                    message = $"Сервис ответил HTTP {statusCode}.";
                    errorKind = "Http";
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            message = ex.Message;
            errorKind = "Timeout";
        }
        catch (HttpRequestException ex)
        {
            message = ex.Message;
            errorKind = "Network";
        }
        catch (Exception ex)
        {
            message = ex.Message;
            errorKind = "Exception";
        }
        finally
        {
            sw.Stop();
        }

        var metrics = s.CheckType.Equals("TCP", StringComparison.OrdinalIgnoreCase)
            ? JsonSerializer.SerializeToElement(new { latencyMs = sw.Elapsed.TotalMilliseconds, host = s.Host, port = s.Port })
            : JsonSerializer.SerializeToElement(new { latencyMs = sw.Elapsed.TotalMilliseconds, endpoint = s.Url, statusCode });

        var item = new CheckResult(s.CheckType.Equals("TCP", StringComparison.OrdinalIgnoreCase)
            ? "TCP availability"
            : "HTTP availability")
        {
            Success = ok,
            DurationMs = sw.Elapsed.TotalMilliseconds,
            WorkerId = ctx.WorkerId,
            IterationIndex = ctx.Iteration,
            ErrorType = ok ? null : errorKind,
            ErrorMessage = ok ? "Проверка доступности прошла успешно." : message,
            StatusCode = statusCode,
            Metrics = metrics
        };

        ctx.Progress.Report(new ProgressUpdate(1, 1, "Проверка доступности"));

        return new ModuleResult
        {
            Results = new List<ResultBase> { item },
            Status = ok ? TestStatus.Success : TestStatus.Failed
        };
    }
}
