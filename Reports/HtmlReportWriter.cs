using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Domain.Reporting;

namespace WebLoadTester.Reports;

public class HtmlReportWriter
{
    public async Task<string> WriteAsync(TestReport report, string htmlPath, CancellationToken ct)
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
        sb.AppendLine("    .cards { display:flex; gap:12px; flex-wrap:wrap; margin:12px 0; }");
        sb.AppendLine("    .card { background:#fff; border:1px solid #d0d0d0; border-radius:6px; padding:10px 14px; min-width:140px; box-shadow:0 1px 2px rgba(0,0,0,0.04); }");
        sb.AppendLine("    .card .label { color:#666; font-size:12px; text-transform:uppercase; letter-spacing:0.4px; }");
        sb.AppendLine("    .card .value { font-size:18px; font-weight:600; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <h1>WebLoadTester Report</h1>");
        sb.AppendLine($"  <div class=\"meta\">Status: {Html(report.Meta.Status)} &nbsp; Started: {report.Meta.StartedAtUtc:u} &nbsp; Finished: {report.Meta.FinishedAtUtc:u} &nbsp; OS: {Html(report.Meta.OsDescription)} &nbsp; App: {Html(report.Meta.AppVersion)}</div>");

        sb.AppendLine("  <h2>Settings</h2>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <tbody>");
        void Row(string name, string value) => sb.AppendLine($"      <tr><th style='width:220px'>{WebUtility.HtmlEncode(name)}</th><td>{WebUtility.HtmlEncode(value)}</td></tr>");

        Row("URL", report.Settings.TargetUrl);
        Row("Test Type", report.Settings.TestType.ToString());
        Row("Total Runs", report.Settings.TotalRuns.ToString());
        Row("Concurrency", report.Settings.Concurrency.ToString());
        Row("Timeout (sec)", report.Settings.TimeoutSeconds.ToString());
        Row("Headless", report.Settings.Headless.ToString());
        Row("Screenshot After Run", report.Settings.ScreenshotAfterRun.ToString());
        Row("Error Policy", report.Settings.StepErrorPolicy.ToString());
        Row("Stress Step", report.Settings.StressStep.ToString());
        Row("Stress Pause (sec)", report.Settings.StressPauseSeconds.ToString());
        Row("Runs Per Level", report.Settings.RunsPerLevel.ToString());
        Row("Endurance (min)", report.Settings.EnduranceMinutes.ToString());
        Row("Selectors", string.Join(", ", report.ScenarioSelectors));

        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");

        sb.AppendLine("  <h2>Summary</h2>");
        sb.AppendLine("  <div class=\"cards\">");
        Card("Total", report.Summary.TotalRunsExecuted.ToString());
        Card("Planned", report.Summary.TotalRunsPlanned.ToString());
        Card("OK", report.Summary.Ok.ToString());
        Card("Fail", report.Summary.Fail.ToString());
        Card("Avg (ms)", report.Summary.AvgMs.ToString("F1"));
        Card("Max (ms)", report.Summary.MaxMs.ToString());
        Card("P95 (ms)", report.Summary.P95.ToString());
        Card("P99 (ms)", report.Summary.P99.ToString());
        Card("Duration (ms)", report.Summary.TotalDurationMs.ToString());
        sb.AppendLine("  </div>");

        sb.AppendLine("  <h2>Phases</h2>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr><th>Name</th><th>Concurrency</th><th>Runs Planned</th><th>Duration (min)</th><th>Pause After (sec)</th></tr></thead>");
        sb.AppendLine("    <tbody>");
        foreach (var phase in report.Phases)
        {
            sb.AppendLine("      <tr>");
            sb.AppendLine($"        <td>{Html(phase.Name)}</td>");
            sb.AppendLine($"        <td>{phase.Concurrency}</td>");
            sb.AppendLine($"        <td>{(phase.Runs?.ToString() ?? "—")}</td>");
            sb.AppendLine($"        <td>{(phase.Duration.HasValue ? phase.Duration.Value.TotalMinutes.ToString("F1") : "—")}</td>");
            sb.AppendLine($"        <td>{phase.PauseAfterSeconds}</td>");
            sb.AppendLine("      </tr>");
        }
        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");

        if (report.PhaseSummaries.Count > 0)
        {
            sb.AppendLine("  <h2>Phase Summaries</h2>");
            sb.AppendLine("  <table>");
            sb.AppendLine("    <thead><tr><th>Phase</th><th>Runs</th><th>Ok</th><th>Fail</th><th>Avg (ms)</th><th>Max (ms)</th><th>P95 (ms)</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var ps in report.PhaseSummaries)
            {
                sb.AppendLine("      <tr>");
                sb.AppendLine($"        <td>{Html(ps.PhaseName)}</td>");
                sb.AppendLine($"        <td>{ps.RunsExecuted}</td>");
                sb.AppendLine($"        <td>{ps.Ok}</td>");
                sb.AppendLine($"        <td>{ps.Fail}</td>");
                sb.AppendLine($"        <td>{ps.AvgMs:F1}</td>");
                sb.AppendLine($"        <td>{ps.MaxMs}</td>");
                sb.AppendLine($"        <td>{ps.P95}</td>");
                sb.AppendLine("      </tr>");
            }
            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
        }

        if (report.ErrorBreakdown.Count > 0)
        {
            sb.AppendLine("  <h2>Error Breakdown</h2>");
            sb.AppendLine("  <table>");
            sb.AppendLine("    <thead><tr><th>Error Type</th><th>Count</th><th>Sample</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var err in report.ErrorBreakdown)
            {
                sb.AppendLine("      <tr>");
                sb.AppendLine($"        <td>{Html(err.ErrorType)}</td>");
                sb.AppendLine($"        <td>{err.Count}</td>");
                sb.AppendLine($"        <td>{Html(err.SampleMessage ?? string.Empty)}</td>");
                sb.AppendLine("      </tr>");
            }
            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
        }

        if (report.TopSlowRuns.Count > 0)
        {
            sb.AppendLine("  <h2>Top Slow Runs</h2>");
            sb.AppendLine("  <table>");
            sb.AppendLine("    <thead><tr><th>RunId</th><th>Worker</th><th>Phase</th><th>Duration (ms)</th><th>Status</th><th>Screenshot</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var run in report.TopSlowRuns)
            {
                var statusClass = run.Success ? "status-ok" : "status-fail";
                var statusText = run.Success ? "OK" : "FAIL";
                sb.AppendLine("      <tr>");
                sb.AppendLine($"        <td>{run.RunId}</td>");
                sb.AppendLine($"        <td>{run.WorkerId}</td>");
                sb.AppendLine($"        <td>{Html(run.Phase)}</td>");
                sb.AppendLine($"        <td>{run.DurationMs}</td>");
                sb.AppendLine($"        <td class=\"{statusClass}\">{statusText}</td>");
                sb.AppendLine($"        <td>{ToLink(run.ScreenshotPath)}</td>");
                sb.AppendLine("      </tr>");
            }
            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
        }

        sb.AppendLine("  <h2>Runs</h2>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr><th>#</th><th>RunId</th><th>WorkerId</th><th>Phase</th><th>Status</th><th>Duration (ms)</th><th>Error</th><th>Screenshot</th></tr></thead>");
        sb.AppendLine("    <tbody>");
        var limitedRuns = report.Runs.OrderBy(r => r.RunId).Take(200).ToList();
        var index = 1;
        foreach (var run in limitedRuns)
        {
            var statusClass = run.Success ? "status-ok" : "status-fail";
            var statusText = run.Success ? "OK" : "FAIL";
            var errorText = run.ErrorMessage ?? string.Empty;
            sb.AppendLine("      <tr>");
            sb.AppendLine($"        <td>{index++}</td>");
            sb.AppendLine($"        <td>{run.RunId}</td>");
            sb.AppendLine($"        <td>{run.WorkerId}</td>");
            sb.AppendLine($"        <td>{Html(string.IsNullOrWhiteSpace(run.PhaseName) ? report.Settings.TestType.ToString() : run.PhaseName)}</td>");
            sb.AppendLine($"        <td class=\"{statusClass}\">{statusText}</td>");
            sb.AppendLine($"        <td>{run.DurationMs}</td>");
            sb.AppendLine($"        <td>{Html(errorText)}</td>");
            sb.AppendLine($"        <td>{ToLink(run.ScreenshotPath)}</td>");
            sb.AppendLine("      </tr>");
        }

        if (report.Runs.Count > limitedRuns.Count)
        {
            sb.AppendLine($"      <tr><td colspan=\"8\">Showing first {limitedRuns.Count} of {report.Runs.Count} runs...</td></tr>");
        }

        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        await File.WriteAllTextAsync(htmlPath, sb.ToString(), ct);
        return htmlPath;

        void Card(string label, string value)
        {
            sb.AppendLine("    <div class=\"card\">");
            sb.AppendLine($"      <div class=\"label\">{Html(label)}</div>");
            sb.AppendLine($"      <div class=\"value\">{Html(value)}</div>");
            sb.AppendLine("    </div>");
        }

        string Html(string text) => WebUtility.HtmlEncode(text ?? string.Empty);

        string ToLink(string? screenshotPath)
        {
            if (string.IsNullOrWhiteSpace(screenshotPath))
            {
                return "-";
            }

            var normalized = screenshotPath.Replace("\\", "/");
            var href = Uri.EscapeUriString($"../{normalized}");
            return $"<a href=\"{href}\" target=\"_blank\">open</a>";
        }
    }
}
