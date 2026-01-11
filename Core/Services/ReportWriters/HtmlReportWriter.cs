using System.Text;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Services.ReportWriters;

public sealed class HtmlReportWriter
{
    public string Write(TestReport report, string path)
    {
        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\" />");
        html.AppendLine("<style>body{font-family:Inter,Segoe UI,Arial,sans-serif;margin:24px;background:#fafafa;color:#111;}table{width:100%;border-collapse:collapse;margin-top:12px;}th,td{border:1px solid #ddd;padding:8px;font-size:13px;}th{background:#f2f2f2;text-align:left;}h1{font-size:20px;} .ok{color:#1a7f37;} .fail{color:#d1242f;}</style>");
        html.AppendLine("</head><body>");
        html.AppendLine($"<h1>{report.Meta.ModuleName} ({report.Meta.ModuleId})</h1>");
        html.AppendLine($"<p>Family: {report.Meta.Family} | Status: {report.Meta.Status} | Started: {report.Meta.StartedAt} | Finished: {report.Meta.FinishedAt}</p>");
        html.AppendLine("<h2>Metrics</h2>");
        html.AppendLine($"<p>Avg: {report.Metrics.AverageMs:F1} ms | P50: {report.Metrics.P50Ms:F1} ms | P95: {report.Metrics.P95Ms:F1} ms | P99: {report.Metrics.P99Ms:F1} ms</p>");
        html.AppendLine("<h2>Results</h2><table><tr><th>Name</th><th>Status</th><th>Duration</th><th>Details</th></tr>");
        foreach (var item in report.Results)
        {
            var status = item.Success ? "OK" : "FAIL";
            var css = item.Success ? "ok" : "fail";
            var details = item.ErrorMessage ?? item.ErrorType ?? item.StatusCode?.ToString() ?? string.Empty;
            html.AppendLine($"<tr><td>{item.Name}</td><td class=\"{css}\">{status}</td><td>{item.DurationMs} ms</td><td>{details}</td></tr>");
        }
        html.AppendLine("</table>");
        html.AppendLine("</body></html>");

        File.WriteAllText(path, html.ToString());
        return path;
    }
}
