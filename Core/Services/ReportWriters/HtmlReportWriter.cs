using System.Text;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Services.ReportWriters;

public sealed class HtmlReportWriter
{
    public string BuildHtml(TestReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\" />");
        sb.AppendLine("<title>WebLoadTester Report</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:Segoe UI,Arial,sans-serif;margin:20px;background:#fafafa;color:#222}");
        sb.AppendLine("table{border-collapse:collapse;width:100%;margin-top:10px}");
        sb.AppendLine("th,td{border:1px solid #ddd;padding:6px 8px;text-align:left}");
        sb.AppendLine("th{background:#f0f0f0}");
        sb.AppendLine(".ok{color:#0a7}");
        sb.AppendLine(".fail{color:#c00}");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine($"<h1>{report.ModuleName} report</h1>");
        sb.AppendLine($"<p>Status: <strong>{report.Status}</strong></p>");
        sb.AppendLine($"<p>Started: {report.StartedAt}</p>");
        sb.AppendLine($"<p>Finished: {report.FinishedAt}</p>");
        sb.AppendLine("<h2>Results</h2>");
        sb.AppendLine("<table>");
        sb.AppendLine("<tr><th>Name</th><th>Success</th><th>Duration</th><th>Error</th></tr>");
        foreach (var result in report.Results)
        {
            var cls = result.Success ? "ok" : "fail";
            sb.AppendLine($"<tr><td>{result.Name}</td><td class=\"{cls}\">{result.Success}</td><td>{result.DurationMs:F0} ms</td><td>{result.ErrorMessage}</td></tr>");
        }
        sb.AppendLine("</table>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }
}
