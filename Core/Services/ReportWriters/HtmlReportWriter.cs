using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Services.ReportWriters;

/// <summary>
/// Писатель HTML-отчётов через хранилище артефактов.
/// </summary>
public class HtmlReportWriter
{
    private readonly IArtifactStore _artifactStore;

    /// <summary>
    /// Создаёт писатель с заданным хранилищем.
    /// </summary>
    public HtmlReportWriter(IArtifactStore artifactStore)
    {
        _artifactStore = artifactStore;
    }

    /// <summary>
    /// Формирует HTML-отчёт и сохраняет его.
    /// </summary>
    public Task<string> WriteAsync(TestReport report, string runId)
    {
        var html = BuildHtml(report);
        return _artifactStore.SaveHtmlReportAsync(runId, html);
    }

    /// <summary>
    /// Собирает HTML-разметку отчёта из результатов и метрик.
    /// </summary>
    private static string BuildHtml(TestReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<html><head><meta charset='utf-8'>");
        sb.AppendLine("<style>body{font-family:Segoe UI,Arial,sans-serif;margin:20px;}table{border-collapse:collapse;width:100%;}th,td{border:1px solid #ccc;padding:6px;}th{background:#f4f4f4;} .fail{color:#b00020;}</style>");
        sb.AppendLine("</head><body>");
        sb.AppendLine($"<h2>{report.ModuleName}</h2>");
        sb.AppendLine($"<p>Status: <strong>{report.Status}</strong></p>");
        sb.AppendLine($"<p>Started: {report.StartedAt:O}<br/>Finished: {report.FinishedAt:O}</p>");
        sb.AppendLine("<h3>Summary</h3>");
        sb.AppendLine("<table><tr><th>Total Items</th><th>Failed Items</th><th>Total Duration (ms)</th><th>Avg</th><th>P95</th><th>P99</th></tr>");
        sb.AppendLine($"<tr><td>{report.Metrics.TotalItems}</td><td>{report.Metrics.FailedItems}</td><td>{report.Metrics.TotalDurationMs:F2}</td><td>{report.Metrics.AverageMs:F2}</td><td>{report.Metrics.P95Ms:F2}</td><td>{report.Metrics.P99Ms:F2}</td></tr></table>");
        sb.AppendLine("<h3>Problems</h3>");
        sb.AppendLine("<table><tr><th>Kind</th><th>Name</th><th>Duration (ms)</th><th>Error</th></tr>");
        foreach (var result in report.Results)
        {
            if (result.Success)
            {
                continue;
            }

            var name = result switch
            {
                RunResult run => run.Name,
                StepResult step => step.Name,
                CheckResult check => check.Name,
                PreflightResult preflight => preflight.Name,
                ProbeResult probe => probe.Name,
                TimingResult timing => timing.Name,
                _ => ""
            };
            var error = string.IsNullOrWhiteSpace(result.ErrorMessage) ? "" : result.ErrorMessage;
            sb.AppendLine($"<tr class='fail'><td>{result.Kind}</td><td>{name}</td><td>{result.DurationMs:F2}</td><td>{error}</td></tr>");
        }
        sb.AppendLine("</table>");
        sb.AppendLine("<h3>Results</h3>");
        sb.AppendLine("<table><tr><th>Kind</th><th>Name</th><th>Success</th><th>Duration (ms)</th><th>Error</th></tr>");
        foreach (var result in report.Results)
        {
            var name = result switch
            {
                RunResult run => run.Name,
                StepResult step => step.Name,
                CheckResult check => check.Name,
                PreflightResult preflight => preflight.Name,
                ProbeResult probe => probe.Name,
                TimingResult timing => timing.Name,
                _ => ""
            };
            var error = string.IsNullOrWhiteSpace(result.ErrorMessage) ? "" : result.ErrorMessage;
            var css = result.Success ? string.Empty : " class='fail'";
            sb.AppendLine($"<tr{css}><td>{result.Kind}</td><td>{name}</td><td>{result.Success}</td><td>{result.DurationMs:F2}</td><td>{error}</td></tr>");
        }
        sb.AppendLine("</table>");
        sb.AppendLine("<h3>Artifacts</h3>");
        sb.AppendLine("<ul>");
        sb.AppendLine("<li>report.json</li>");
        if (!string.IsNullOrWhiteSpace(report.Artifacts.HtmlPath))
        {
            sb.AppendLine("<li>report.html</li>");
        }
        sb.AppendLine("<li>logs/run.log</li>");
        var screenshots = report.Results
            .Select(result => result switch
            {
                RunResult run => run.ScreenshotPath,
                StepResult step => step.ScreenshotPath,
                _ => null
            })
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct()
            .ToList();
        foreach (var screenshot in screenshots)
        {
            sb.AppendLine($"<li>{screenshot}</li>");
        }
        foreach (var artifact in report.ModuleArtifacts)
        {
            sb.AppendLine($"<li>{artifact.RelativePath}</li>");
        }
        sb.AppendLine("</ul>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }
}
