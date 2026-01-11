using System.Text;
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

    public Task<string> WriteAsync(TestReport report, string runFolder)
    {
        var html = BuildHtml(report);
        return _artifactStore.SaveHtmlAsync(report, runFolder, html);
    }

    private static string BuildHtml(TestReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<html><head><meta charset='utf-8'>");
        sb.AppendLine("<style>body{font-family:Segoe UI,Arial,sans-serif;margin:20px;}table{border-collapse:collapse;width:100%;}th,td{border:1px solid #ccc;padding:6px;}th{background:#f4f4f4;} .fail{color:#b00020;}</style>");
        sb.AppendLine("</head><body>");
        sb.AppendLine($"<h2>{report.ModuleName}</h2>");
        sb.AppendLine($"<p>Status: <strong>{report.Status}</strong></p>");
        sb.AppendLine($"<p>Started: {report.StartedAt:O}<br/>Finished: {report.FinishedAt:O}</p>");
        sb.AppendLine("<h3>Metrics</h3>");
        sb.AppendLine("<table><tr><th>Avg</th><th>Min</th><th>Max</th><th>P50</th><th>P95</th><th>P99</th></tr>");
        sb.AppendLine($"<tr><td>{report.Metrics.AverageMs:F2}</td><td>{report.Metrics.MinMs:F2}</td><td>{report.Metrics.MaxMs:F2}</td><td>{report.Metrics.P50Ms:F2}</td><td>{report.Metrics.P95Ms:F2}</td><td>{report.Metrics.P99Ms:F2}</td></tr></table>");
        sb.AppendLine("<h3>Results</h3>");
        sb.AppendLine("<table><tr><th>Kind</th><th>Name</th><th>Success</th><th>Duration (ms)</th><th>Error</th></tr>");
        foreach (var result in report.Results)
        {
            var name = result switch
            {
                RunResult run => run.Name,
                CheckResult check => check.Name,
                ProbeResult probe => probe.Name,
                TimingResult timing => timing.Name,
                _ => ""
            };
            var error = string.IsNullOrWhiteSpace(result.ErrorMessage) ? "" : result.ErrorMessage;
            var css = result.Success ? string.Empty : " class='fail'";
            sb.AppendLine($"<tr{css}><td>{result.Kind}</td><td>{name}</td><td>{result.Success}</td><td>{result.DurationMs:F2}</td><td>{error}</td></tr>");
        }
        sb.AppendLine("</table>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }
}
