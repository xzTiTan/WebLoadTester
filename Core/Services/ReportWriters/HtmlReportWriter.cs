using System.IO;
using System.Text;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Services.ReportWriters;

public static class HtmlReportWriter
{
    public static async Task<string> WriteAsync(TestReport report, IArtifactStore store)
    {
        var html = BuildHtml(report);
        var path = await store.SaveHtmlAsync(report, html).ConfigureAwait(false);
        await File.WriteAllTextAsync(path, html).ConfigureAwait(false);
        return path;
    }

    private static string BuildHtml(TestReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html>");
        sb.AppendLine("<html><head><meta charset='utf-8'>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:Segoe UI,Arial,sans-serif;padding:20px;background:#f8f8f8;}");
        sb.AppendLine("table{border-collapse:collapse;width:100%;background:#fff;}");
        sb.AppendLine("th,td{border:1px solid #ddd;padding:8px;font-size:13px;}");
        sb.AppendLine("th{background:#eee;text-align:left;}");
        sb.AppendLine(".ok{color:#2e7d32;font-weight:600;}");
        sb.AppendLine(".fail{color:#c62828;font-weight:600;}");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine($"<h1>{report.ModuleName}</h1>");
        sb.AppendLine($"<p>Status: <strong>{report.Status}</strong></p>");
        sb.AppendLine($"<p>Started: {report.StartedAt:u}<br/>Finished: {report.FinishedAt:u}</p>");
        if (report.Metrics is not null)
        {
            sb.AppendLine("<h2>Metrics</h2>");
            sb.AppendLine("<ul>");
            sb.AppendLine($"<li>Avg: {report.Metrics.AvgMs:F2} ms</li>");
            sb.AppendLine($"<li>P50: {report.Metrics.P50Ms:F2} ms</li>");
            sb.AppendLine($"<li>P95: {report.Metrics.P95Ms:F2} ms</li>");
            sb.AppendLine($"<li>P99: {report.Metrics.P99Ms:F2} ms</li>");
            sb.AppendLine("</ul>");
        }
        sb.AppendLine("<h2>Results</h2>");
        sb.AppendLine("<table><thead><tr><th>Name</th><th>Status</th><th>Duration (ms)</th><th>Error</th></tr></thead><tbody>");
        foreach (var result in report.Results)
        {
            var cls = result.Success ? "ok" : "fail";
            sb.AppendLine($"<tr><td>{result.Name}</td><td class='{cls}'>{(result.Success ? "OK" : "FAIL")}</td><td>{result.DurationMs:F2}</td><td>{result.ErrorMessage}</td></tr>");
        }
        sb.AppendLine("</tbody></table>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }
}
