using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Domain;

namespace WebLoadTester.Reports;

public class HtmlReportWriter
{
    public async Task<string> WriteAsync(ReportDocument doc, string htmlPath, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var directory = Path.GetDirectoryName(htmlPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\" />");
        sb.AppendLine("  <title>WebLoadTester Report</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    body { font-family: 'Segoe UI', Arial, sans-serif; background:#f7f7f7; color:#222; margin:20px; }");
        sb.AppendLine("    h1 { margin-bottom:4px; }");
        sb.AppendLine("    h2 { margin-top:24px; }");
        sb.AppendLine("    table { border-collapse: collapse; width: 100%; background:#fff; }");
        sb.AppendLine("    th, td { border: 1px solid #d0d0d0; padding: 8px; text-align: left; }");
        sb.AppendLine("    th { background: #efefef; }");
        sb.AppendLine("    .status-ok { color: #0a7d00; font-weight: 600; }");
        sb.AppendLine("    .status-fail { color: #b00020; font-weight: 600; }");
        sb.AppendLine("    .pill { display:inline-block; padding:2px 6px; border-radius:4px; background:#ececec; margin-left:4px; }");
        sb.AppendLine("    .meta { margin-bottom:12px; color:#555; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <h1>WebLoadTester Report</h1>");
        sb.AppendLine($"  <div class=\"meta\">Started: {doc.Meta.StartedAt:u} &nbsp; Finished: {doc.Meta.FinishedAt:u}</div>");

        sb.AppendLine("  <h2>Settings</h2>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <tbody>");
        void Row(string name, string value) => sb.AppendLine($"      <tr><th style='width:220px'>{System.Net.WebUtility.HtmlEncode(name)}</th><td>{System.Net.WebUtility.HtmlEncode(value)}</td></tr>");

        Row("URL", doc.Settings.TargetUrl);
        Row("Test Type", doc.TestType.ToString());
        Row("Total Runs", doc.Settings.TotalRuns.ToString());
        Row("Concurrency", doc.Settings.Concurrency.ToString());
        Row("Timeout (sec)", doc.Settings.TimeoutSeconds.ToString());
        Row("Headless", doc.Settings.Headless.ToString());
        Row("Screenshot After Run", doc.Settings.ScreenshotAfterRun.ToString());
        Row("Error Policy", doc.Settings.StepErrorPolicy.ToString());
        Row("Stress Step", doc.Settings.StressStep.ToString());
        Row("Stress Pause (sec)", doc.Settings.StressPauseSeconds.ToString());
        Row("Runs Per Level", doc.Settings.RunsPerLevel.ToString());
        Row("Endurance (min)", doc.Settings.EnduranceMinutes.ToString());

        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");

        sb.AppendLine("  <h2>Summary</h2>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <tbody>");
        Row("Total", doc.Summary.TotalRuns.ToString());
        Row("OK", doc.Summary.Ok.ToString());
        Row("Fail", doc.Summary.Fail.ToString());
        Row("Avg (ms)", doc.Summary.AvgDurationMs.ToString("F1"));
        Row("Min (ms)", doc.Summary.MinDurationMs.ToString("F1"));
        Row("Max (ms)", doc.Summary.MaxDurationMs.ToString("F1"));
        Row("P95 (ms)", doc.Summary.P95DurationMs.ToString("F1"));
        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");

        sb.AppendLine("  <h2>Phases</h2>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr><th>Name</th><th>Concurrency</th><th>Runs Planned</th><th>Duration (min)</th><th>Pause After (sec)</th></tr></thead>");
        sb.AppendLine("    <tbody>");
        foreach (var phase in doc.Phases)
        {
            sb.AppendLine("      <tr>");
            sb.AppendLine($"        <td>{System.Net.WebUtility.HtmlEncode(phase.Name)}</td>");
            sb.AppendLine($"        <td>{phase.Concurrency}</td>");
            sb.AppendLine($"        <td>{(phase.Runs?.ToString() ?? "—")}</td>");
            sb.AppendLine($"        <td>{(phase.Duration.HasValue ? phase.Duration.Value.TotalMinutes.ToString("F1") : "—")}</td>");
            sb.AppendLine($"        <td>{phase.PauseAfterSeconds}</td>");
            sb.AppendLine("      </tr>");
        }
        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");

        sb.AppendLine("  <h2>Runs</h2>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr><th>RunId</th><th>WorkerId</th><th>Phase</th><th>Status</th><th>Duration (ms)</th><th>Error</th><th>Screenshot</th></tr></thead>");
        sb.AppendLine("    <tbody>");
        var htmlDir = string.IsNullOrWhiteSpace(directory) ? Directory.GetCurrentDirectory() : directory;
        foreach (var run in doc.Runs.OrderBy(r => r.RunId))
        {
            var statusClass = run.Success ? "status-ok" : "status-fail";
            var statusText = run.Success ? "OK" : "FAIL";
            var errorText = run.ErrorMessage ?? string.Empty;
            var durationMs = run.Duration.TotalMilliseconds.ToString("F0");
            var screenshotCell = string.Empty;

            if (!string.IsNullOrWhiteSpace(run.ScreenshotPath))
            {
                var relative = Path.GetRelativePath(htmlDir, run.ScreenshotPath);
                relative = relative.Replace("\\", "/");
                screenshotCell = $"<a href=\"{Uri.EscapeUriString(relative)}\">open</a>";
            }

            sb.AppendLine("      <tr>");
            sb.AppendLine($"        <td>{run.RunId}</td>");
            sb.AppendLine($"        <td>{run.WorkerId}</td>");
            sb.AppendLine($"        <td>{WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(run.PhaseName) ? doc.TestType.ToString() : run.PhaseName)}</td>");
            sb.AppendLine($"        <td class=\"{statusClass}\">{statusText}</td>");
            sb.AppendLine($"        <td>{durationMs}</td>");
            sb.AppendLine($"        <td>{WebUtility.HtmlEncode(errorText)}</td>");
            sb.AppendLine($"        <td>{screenshotCell}</td>");
            sb.AppendLine("      </tr>");
        }
        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        await File.WriteAllTextAsync(htmlPath, sb.ToString(), ct);
        return htmlPath;
    }
}
