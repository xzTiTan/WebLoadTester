using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Services.ReportWriters;

public class HtmlReportWriter
{
    private readonly IArtifactStore _artifactStore;

    public HtmlReportWriter(IArtifactStore artifactStore)
    {
        _artifactStore = artifactStore;
    }

    public Task<string> WriteAsync(TestReport report, string runId)
    {
        var html = BuildHtml(report);
        return _artifactStore.SaveHtmlReportAsync(runId, html);
    }

    private static string BuildHtml(TestReport report)
    {
        var failed = report.Results.Where(r => !r.Success).ToList();
        var severity = GetSeverity(report.Metrics.FailedItems, report.Metrics.TotalItems);
        var recommendations = BuildRecommendations(report, failed);

        var sb = new StringBuilder();
        sb.AppendLine("<html><head><meta charset='utf-8'>");
        sb.AppendLine("<style>body{font-family:Segoe UI,Arial,sans-serif;margin:20px;line-height:1.4}table{border-collapse:collapse;width:100%;margin-bottom:12px;}th,td{border:1px solid #d2d2d2;padding:6px;vertical-align:top}th{background:#f6f7f9} .fail{color:#b00020}.ok{color:#0a7d2e}.badge{display:inline-block;padding:2px 8px;border-radius:12px;background:#eee}.sev-high{background:#ffd9df}.sev-medium{background:#fff2cc}.sev-low{background:#e7f5e8}.grid{display:grid;grid-template-columns:1fr 1fr;gap:10px}.bar{background:#e9ecef;height:18px;border-radius:8px;overflow:hidden}.bar>span{display:block;height:100%;background:#1f6feb}</style>");
        sb.AppendLine("</head><body>");
        sb.AppendLine($"<h2>{Escape(report.ModuleName)}</h2>");
        sb.AppendLine($"<p><span class='badge {severity.CssClass}'>Критичность: {severity.Label}</span></p>");
        sb.AppendLine($"<p>Статус: <strong class='{(report.Status == TestStatus.Success ? "ok" : "fail")}'>{report.Status}</strong></p>");
        sb.AppendLine($"<p>Начало: {report.StartedAt:O}<br/>Завершение: {report.FinishedAt:O}</p>");

        sb.AppendLine("<h3>Сводка результата</h3>");
        sb.AppendLine("<div class='grid'>");
        sb.AppendLine($"<div><strong>Всего проверок:</strong> {report.Metrics.TotalItems}<br/><strong>Ошибок:</strong> {report.Metrics.FailedItems}</div>");
        sb.AppendLine($"<div><strong>Среднее, мс:</strong> {report.Metrics.AverageMs:F2}<br/><strong>P95/P99, мс:</strong> {report.Metrics.P95Ms:F2} / {report.Metrics.P99Ms:F2}</div>");
        sb.AppendLine("</div>");

        var successPercent = report.Metrics.TotalItems > 0
            ? (report.Metrics.TotalItems - report.Metrics.FailedItems) * 100.0 / report.Metrics.TotalItems
            : 0;
        sb.AppendLine("<p>Диаграмма успешности:</p>");
        sb.AppendLine($"<div class='bar'><span style='width:{successPercent.ToString("F2", CultureInfo.InvariantCulture)}%'></span></div>");
        sb.AppendLine($"<p>Успешно: {successPercent:F1}%</p>");

        sb.AppendLine("<h3>Найденные проблемы</h3>");
        sb.AppendLine("<table><tr><th>Критичность</th><th>Тип</th><th>Элемент</th><th>Длительность (мс)</th><th>Ошибка</th></tr>");
        foreach (var result in failed)
        {
            var name = GetResultName(result);
            var itemSeverity = ResolveItemSeverity(result.ErrorType, result.ErrorMessage);
            sb.AppendLine($"<tr class='fail'><td>{itemSeverity}</td><td>{Escape(result.Kind)}</td><td>{Escape(name)}</td><td>{result.DurationMs:F2}</td><td>{Escape(result.ErrorMessage ?? string.Empty)}</td></tr>");
        }
        if (failed.Count == 0)
        {
            sb.AppendLine("<tr><td colspan='5' class='ok'>Проблемы не обнаружены.</td></tr>");
        }
        sb.AppendLine("</table>");

        sb.AppendLine("<h3>Рекомендации по устранению</h3><ul>");
        foreach (var recommendation in recommendations)
        {
            sb.AppendLine($"<li>{Escape(recommendation)}</li>");
        }
        sb.AppendLine("</ul>");

        sb.AppendLine("<h3>Матрица результатов</h3>");
        sb.AppendLine("<table><tr><th>Тип</th><th>Название</th><th>Успех</th><th>Длительность (мс)</th><th>Детали</th></tr>");
        foreach (var result in report.Results)
        {
            var css = result.Success ? string.Empty : " class='fail'";
            sb.AppendLine($"<tr{css}><td>{Escape(result.Kind)}</td><td>{Escape(GetResultName(result))}</td><td>{(result.Success ? "Да" : "Нет")}</td><td>{result.DurationMs:F2}</td><td>{Escape(RenderDetails(result.DetailsJson))}</td></tr>");
        }
        sb.AppendLine("</table>");

        sb.AppendLine("<h3>Артефакты</h3><ul><li>report.json</li>");
        if (!string.IsNullOrWhiteSpace(report.Artifacts.HtmlPath))
        {
            sb.AppendLine("<li>report.html</li>");
        }
        sb.AppendLine("<li>logs/run.log</li>");
        foreach (var artifact in report.ModuleArtifacts)
        {
            sb.AppendLine($"<li>{Escape(artifact.RelativePath)}</li>");
        }
        sb.AppendLine("</ul></body></html>");
        return sb.ToString();
    }

    private static string RenderDetails(string? detailsJson)
    {
        if (string.IsNullOrWhiteSpace(detailsJson)) return string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(detailsJson);
            if (doc.RootElement.TryGetProperty("profile", out var profile))
            {
                return $"Профиль: {profile}";
            }
        }
        catch { }
        return detailsJson.Length > 220 ? detailsJson[..220] + "..." : detailsJson;
    }

    private static (string Label, string CssClass) GetSeverity(int failed, int total)
    {
        if (failed <= 0) return ("Низкая", "sev-low");
        var ratio = total <= 0 ? 1.0 : (double)failed / total;
        if (ratio >= 0.5) return ("Высокая", "sev-high");
        return ("Средняя", "sev-medium");
    }

    private static List<string> BuildRecommendations(TestReport report, List<ResultBase> failed)
    {
        var list = new List<string>();
        if (failed.Count == 0)
        {
            list.Add("Сохраните текущий прогон как эталон для повторной проверки и регрессионного сравнения.");
            return list;
        }

        if (failed.Any(f => (f.ErrorType ?? string.Empty).Contains("Timeout", StringComparison.OrdinalIgnoreCase)))
            list.Add("Увеличьте timeout и проверьте сетевую задержку/доступность внешних зависимостей.");
        if (failed.Any(f => (f.ErrorMessage ?? string.Empty).Contains("selector", StringComparison.OrdinalIgnoreCase)))
            list.Add("Проверьте актуальность CSS/XPath селекторов и стабильность DOM-структуры страниц.");
        if (report.ModuleId == "ui.timing")
            list.Add("Сравните результаты профилей совместимости: различия по browser/viewport требуют адаптивной доработки интерфейса.");
        if (report.ModuleId == "ui.scenario")
            list.Add("Для регрессионной проверки повторите сценарий после исправлений и сопоставьте шаги с последним успешным прогоном.");
        if (report.ModuleId.StartsWith("http.", StringComparison.OrdinalIgnoreCase))
            list.Add("Проверьте корректность контрактов API, коды ответов и заголовки кеширования.");

        if (list.Count == 0)
            list.Add("Проанализируйте лог run.log и DetailsJson проблемных элементов, затем повторите прогон.");
        return list;
    }

    private static string ResolveItemSeverity(string? errorType, string? errorMessage)
    {
        var source = $"{errorType} {errorMessage}";
        if (source.Contains("timeout", StringComparison.OrdinalIgnoreCase) || source.Contains("dns", StringComparison.OrdinalIgnoreCase)) return "Высокая";
        if (source.Contains("assert", StringComparison.OrdinalIgnoreCase) || source.Contains("selector", StringComparison.OrdinalIgnoreCase)) return "Средняя";
        return "Низкая";
    }

    private static string GetResultName(ResultBase result) => result switch
    {
        RunResult run => run.Name,
        StepResult step => step.Name,
        CheckResult check => check.Name,
        PreflightResult preflight => preflight.Name,
        ProbeResult probe => probe.Name,
        TimingResult timing => timing.Name,
        _ => string.Empty
    };

    private static string Escape(string? value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
}
