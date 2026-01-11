using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Core.Services.Metrics;
using WebLoadTester.Core.Services.ReportWriters;

namespace WebLoadTester.Core.Services;

/// <summary>
/// Оркестратор: валидирует настройки, запускает модуль и сохраняет отчёты.
/// </summary>
public class TestOrchestrator
{
    private readonly JsonReportWriter _jsonWriter;
    private readonly HtmlReportWriter _htmlWriter;

    /// <summary>
    /// Создаёт оркестратор с писателями отчётов.
    /// </summary>
    public TestOrchestrator(JsonReportWriter jsonWriter, HtmlReportWriter htmlWriter)
    {
        _jsonWriter = jsonWriter;
        _htmlWriter = htmlWriter;
    }

    /// <summary>
    /// Запускает модуль, обрабатывает ошибки и возвращает итоговый отчёт.
    /// </summary>
    public async Task<TestReport> RunAsync(ITestModule module, object settings, RunContext context, CancellationToken ct)
    {
        var validation = module.Validate(settings);
        if (validation.Count > 0)
        {
            var report = CreateBaseReport(module, settings, context);
            report.Status = TestStatus.Error;
            report.Results = new List<ResultBase>
            {
                new CheckResult("Validation")
                {
                    Success = false,
                    DurationMs = 0,
                    ErrorType = "Validation",
                    ErrorMessage = string.Join("; ", validation)
                }
            };
            report.Metrics = MetricsCalculator.Calculate(report.Results);
            return await FinalizeReportAsync(report, context, ct);
        }

        context.Log.Info($"Starting module {module.DisplayName}");
        TestReport resultReport;
        try
        {
            resultReport = await module.RunAsync(settings, context, ct);
            resultReport.Status = ct.IsCancellationRequested ? TestStatus.Stopped : resultReport.Status;
        }
        catch (OperationCanceledException)
        {
            context.Log.Warn("Run cancelled.");
            resultReport = CreateBaseReport(module, settings, context);
            resultReport.Status = TestStatus.Stopped;
        }
        catch (Exception ex)
        {
            context.Log.Error($"Run failed: {ex.Message}");
            resultReport = CreateBaseReport(module, settings, context);
            resultReport.Status = TestStatus.Error;
            resultReport.Results.Add(new CheckResult("Exception")
            {
                Success = false,
                DurationMs = 0,
                ErrorType = ex.GetType().Name,
                ErrorMessage = ex.ToString()
            });
        }

        if (resultReport.Results.Count > 0)
        {
            resultReport.Metrics = MetricsCalculator.Calculate(resultReport.Results);
        }

        return await FinalizeReportAsync(resultReport, context, ct);
    }

    /// <summary>
    /// Финализирует отчёт: сохраняет артефакты и обновляет прогресс.
    /// </summary>
    private async Task<TestReport> FinalizeReportAsync(TestReport report, RunContext context, CancellationToken ct)
    {
        var runFolder = context.Artifacts.CreateRunFolder(report.StartedAt.ToString("yyyyMMdd_HHmmss"));
        report.Artifacts.ScreenshotsFolder = runFolder;
        report.Artifacts.JsonPath = await _jsonWriter.WriteAsync(report, runFolder);
        report.Artifacts.HtmlPath = await _htmlWriter.WriteAsync(report, runFolder);
        context.Log.Info($"Report saved: {report.Artifacts.JsonPath}");
        context.Progress.Report(new ProgressUpdate(0, 0, "Completed"));
        return report;
    }

    /// <summary>
    /// Создаёт базовый отчёт с метаданными и снимком настроек.
    /// </summary>
    private static TestReport CreateBaseReport(ITestModule module, object settings, RunContext context)
    {
        return new TestReport
        {
            ModuleId = module.Id,
            ModuleName = module.DisplayName,
            Family = module.Family,
            StartedAt = context.Now,
            FinishedAt = context.Now,
            Status = TestStatus.Completed,
            AppVersion = typeof(TestOrchestrator).Assembly.GetName().Version?.ToString() ?? "",
            OsDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            SettingsSnapshot = JsonSerializer.Serialize(settings)
        };
    }
}
