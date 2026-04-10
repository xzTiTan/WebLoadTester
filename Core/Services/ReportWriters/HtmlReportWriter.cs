using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
        var title = string.IsNullOrWhiteSpace(report.FinalName) ? report.TestName : report.FinalName;
        var failed = report.Results.Where(x => !x.Success).ToList();
        var passed = Math.Max(0, report.Metrics.TotalItems - report.Metrics.FailedItems);
        var successPercent = report.Metrics.TotalItems > 0 ? passed * 100d / report.Metrics.TotalItems : (report.Status == TestStatus.Success ? 100d : 0d);
        var severity = GetSeverity(report.Metrics.FailedItems, report.Metrics.TotalItems);
        var recommendations = BuildRecommendations(report, failed);

        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html><html lang='ru'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1'>");
        sb.AppendLine($"<title>{Escape(title)} - {Escape(report.ModuleName)}</title>");
        sb.AppendLine("<style>:root{--bg:#f4f6fb;--p:#fff;--soft:#f7f9fc;--line:#d9e1ec;--text:#18212f;--muted:#66758a;--ok:#1f7a49;--fail:#b42318;--warn:#b7791f;--accent:#0f6cbd}*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font:14px/1.5 Segoe UI,Arial,sans-serif;padding:24px}.page{max-width:1260px;margin:0 auto}.hero,.sec,.card{background:var(--p);border:1px solid var(--line);border-radius:18px;box-shadow:0 10px 28px rgba(15,28,63,.06)}.hero{padding:24px;margin-bottom:18px}.hero h1{margin:0 0 6px;font-size:30px}.sub{color:var(--muted)}.row{display:flex;gap:12px;flex-wrap:wrap;align-items:flex-start;justify-content:space-between}.badges,.cards,.artifacts,.gallery,.diag{display:grid;gap:12px}.badges{grid-template-columns:repeat(auto-fit,minmax(180px,1fr));margin-top:16px}.cards{grid-template-columns:repeat(auto-fit,minmax(190px,1fr));margin-bottom:18px}.card{padding:16px}.sec{padding:18px 20px;margin:0 0 18px}.sec h2,.card h3{margin:0 0 10px}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:10px 14px}.kv{padding:10px 12px;border:1px solid var(--line);border-radius:12px;background:var(--soft)}.k{display:block;color:var(--muted);font-size:12px;text-transform:uppercase}.v{font-weight:600;word-break:break-word}.badge{display:inline-flex;padding:5px 10px;border-radius:999px;border:1px solid var(--line);background:var(--soft);font-weight:700}.b-ok{color:var(--ok);background:#e8f5ed}.b-fail{color:var(--fail);background:#fdecec}.b-warn{color:var(--warn);background:#fff4df}.b-low{background:#edf8ef}.b-mid{background:#fff4df}.b-high{background:#fdecec}.bar{height:12px;border-radius:999px;background:#e7edf5;overflow:hidden}.bar>span{display:block;height:100%;background:linear-gradient(90deg,var(--accent),#5a95d6)}.bar.ok>span{background:linear-gradient(90deg,#2d935e,#65c18c)}.bar.fail>span{background:linear-gradient(90deg,#c53b32,#ea6f65)}.bar.warn>span{background:linear-gradient(90deg,#cf7c10,#f0aa52)}.cap{display:flex;justify-content:space-between;font-size:12px;color:var(--muted);margin-top:6px;gap:10px}table{width:100%;border-collapse:collapse}th,td{padding:10px 12px;border-bottom:1px solid var(--line);vertical-align:top;text-align:left}th{background:var(--soft);color:var(--muted);font-size:12px;text-transform:uppercase}tr:last-child td{border-bottom:none}.ok{color:var(--ok);font-weight:700}.fail{color:var(--fail);font-weight:700}.warn{color:var(--warn);font-weight:700}.artifacts{grid-template-columns:repeat(auto-fit,minmax(220px,1fr))}.artifacts a{display:block;padding:12px 14px;border:1px solid var(--line);border-radius:14px;background:var(--soft);text-decoration:none;color:inherit}.gallery{grid-template-columns:repeat(auto-fit,minmax(230px,1fr))}.shot{border:1px solid var(--line);border-radius:16px;overflow:hidden;background:var(--soft)}.shot img{display:block;width:100%;height:180px;object-fit:cover;background:#dfe6f1}.shot .body{padding:12px 14px}.pill{display:inline-block;padding:3px 8px;border:1px solid var(--line);border-radius:999px;background:var(--soft);font-size:12px;margin:4px 6px 0 0}.diag{grid-template-columns:repeat(auto-fit,minmax(230px,1fr))}.small{font-size:12px;color:var(--muted)}.empty{padding:14px;border:1px dashed var(--line);border-radius:14px;background:var(--soft);color:var(--muted)}.mono{font-family:Consolas,'Courier New',monospace}.card .n{font-size:28px;font-weight:800;line-height:1.1;margin-bottom:4px}@media print{body{padding:0;background:#fff}.hero,.sec,.card{box-shadow:none}a{text-decoration:none;color:inherit}}</style>");
        sb.AppendLine("</head><body><div class='page'>");
        sb.AppendLine($"<section class='hero'><div class='row'><div><h1>{Escape(title)}</h1><div class='sub'>{Escape(report.ModuleName)} · {Escape(report.ModuleId)} · {Escape(report.Family.ToString())}</div><div style='margin-top:12px'><span class='badge {(report.Status == TestStatus.Success ? "b-ok" : report.Status == TestStatus.Cancelled ? "b-warn" : "b-fail")}'>{Escape(FormatStatus(report.Status))}</span> <span class='badge {severity.CssClass}'>{Escape(severity.Label)} severity</span> <span class='badge mono'>RunId: {Escape(report.RunId)}</span></div></div><div style='min-width:280px;max-width:420px;width:100%'><div class='bar ok'><span style='width:{ClampPercent(successPercent):F1}%'></span></div><div class='cap'><span>Passed: {passed}</span><span>Failed: {failed.Count}</span><span>{successPercent:F1}% success</span></div></div></div>");
        sb.AppendLine("<div class='badges'>");
        AppendKeyMetric("Started", FormatDate(report.StartedAt), sb);
        AppendKeyMetric("Finished", FormatDate(report.FinishedAt), sb);
        AppendKeyMetric("Duration", FormatDuration(report.Metrics.TotalDurationMs), sb);
        AppendKeyMetric("Average / P95", $"{FormatDuration(report.Metrics.AverageMs)} / {FormatDuration(report.Metrics.P95Ms)}", sb);
        AppendKeyMetric("Items", report.Metrics.TotalItems.ToString(CultureInfo.InvariantCulture), sb);
        AppendKeyMetric("Failed items", report.Metrics.FailedItems.ToString(CultureInfo.InvariantCulture), sb);
        sb.AppendLine("</div></section>");
        sb.AppendLine("<div class='cards'>");
        AppendSummaryCard("Status", FormatStatus(report.Status), "Final module result.", sb);
        AppendSummaryCard("Success rate", $"{successPercent:F1}%", "Passed items against total checks.", sb);
        AppendSummaryCard("Latency", FormatDuration(report.Metrics.AverageMs), $"Min {FormatDuration(report.Metrics.MinMs)}, max {FormatDuration(report.Metrics.MaxMs)}.", sb);
        AppendSummaryCard("Errors", report.Metrics.FailedItems.ToString(CultureInfo.InvariantCulture), report.Metrics.ErrorBreakdown.Count == 0 ? "No recorded error categories." : string.Join(", ", report.Metrics.ErrorBreakdown.OrderByDescending(x => x.Value).Select(x => $"{x.Key}: {x.Value}")), sb);
        sb.AppendLine("</div>");
        AppendSectionHeader("Run metadata", null, sb);
        sb.AppendLine("<div class='grid'>");
        AppendKv("Run ID", report.RunId, sb); AppendKv("Module", report.ModuleName, sb); AppendKv("Module ID", report.ModuleId, sb); AppendKv("Family", report.Family.ToString(), sb); AppendKv("App version", string.IsNullOrWhiteSpace(report.AppVersion) ? "n/a" : report.AppVersion, sb); AppendKv("OS", string.IsNullOrWhiteSpace(report.OsDescription) ? "n/a" : report.OsDescription, sb);
        sb.AppendLine("</div></section>");
        AppendSectionHeader("Run parameters", null, sb);
        sb.AppendLine("<div class='grid'>");
        AppendKv("Mode", report.ProfileSnapshot.Mode.ToString(), sb); AppendKv("Parallelism", report.ProfileSnapshot.Parallelism.ToString(CultureInfo.InvariantCulture), sb); AppendKv("Iterations", report.ProfileSnapshot.Iterations.ToString(CultureInfo.InvariantCulture), sb); AppendKv("Duration (s)", report.ProfileSnapshot.DurationSeconds.ToString(CultureInfo.InvariantCulture), sb); AppendKv("Timeout (s)", report.ProfileSnapshot.TimeoutSeconds.ToString(CultureInfo.InvariantCulture), sb); AppendKv("Pause (ms)", report.ProfileSnapshot.PauseBetweenIterationsMs.ToString(CultureInfo.InvariantCulture), sb); AppendKv("Headless", FormatBool(report.ProfileSnapshot.Headless), sb); AppendKv("Screenshots", report.ProfileSnapshot.ScreenshotsPolicy.ToString(), sb);
        foreach (var pair in DescribeModuleSettings(report)) AppendKv(pair.Key, pair.Value, sb);
        sb.AppendLine("</div></section>");
        AppendSectionHeader("Artifacts", "Open the files produced during the run.", sb);
        var artifactPaths = BuildArtifactPaths(report);
        if (artifactPaths.Count == 0) sb.AppendLine("<div class='empty'>No artifact links were recorded.</div>");
        else { sb.AppendLine("<div class='artifacts'>"); foreach (var artifactPath in artifactPaths) sb.AppendLine($"<a href='{Escape(artifactPath)}'><strong>{Escape(GuessArtifactLabel(artifactPath))}</strong><div class='small'>{Escape(artifactPath)}</div></a>"); sb.AppendLine("</div>"); }
        sb.AppendLine("</section>");
        AppendHttpPerformanceSection(report, sb); AppendSecurityChecksSection(report, sb); AppendHttpFunctionalSection(report, sb); AppendAssetsSection(report, sb); AppendDiagnosticsSection(report, sb); AppendAvailabilitySection(report, sb); AppendPreflightSection(report, sb); AppendSnapshotSection(report, sb); AppendTimingSection(report, sb); AppendScenarioSection(report, sb);
        AppendSectionHeader("Key findings", null, sb);
        if (failed.Count == 0) sb.AppendLine("<div class='empty'>No failed items were recorded.</div>");
        else { sb.AppendLine("<table><tr><th>Severity</th><th>Type</th><th>Item</th><th>Duration</th><th>Message</th></tr>"); foreach (var item in failed.Take(25)) sb.AppendLine($"<tr><td>{Escape(ResolveItemSeverity(item.ErrorType, item.ErrorMessage))}</td><td>{Escape(item.Kind)}</td><td>{Escape(GetResultName(item))}</td><td>{Escape(FormatDuration(item.DurationMs))}</td><td>{Escape(string.IsNullOrWhiteSpace(item.ErrorMessage) ? item.ErrorType ?? "Failure recorded." : item.ErrorMessage)}</td></tr>"); sb.AppendLine("</table>"); }
        sb.AppendLine("</section>");
        AppendSectionHeader("Recommendations", null, sb);
        sb.AppendLine("<ul>");
        foreach (var recommendation in recommendations) sb.AppendLine($"<li>{Escape(recommendation)}</li>");
        sb.AppendLine("</ul></section>");
        if (report.Metrics.TopSlow.Count > 0)
        {
            var maxSlow = Math.Max(1d, report.Metrics.TopSlow.Max(x => x.DurationMs));
            AppendSectionHeader("Slowest items", null, sb);
            sb.AppendLine("<table><tr><th>Item</th><th>Type</th><th>Status</th><th>Duration</th><th>Impact</th></tr>");
            foreach (var item in report.Metrics.TopSlow)
            {
                sb.AppendLine($"<tr><td>{Escape(GetResultName(item))}</td><td>{Escape(item.Kind)}</td><td class='{(item.Success ? "ok" : "fail")}'>{(item.Success ? "Pass" : "Fail")}</td><td>{Escape(FormatDuration(item.DurationMs))}</td><td>{RenderBar(item.DurationMs, maxSlow, FormatDuration(item.DurationMs), item.Success ? "ok" : "fail")}</td></tr>");
            }
            sb.AppendLine("</table></section>");
        }
        AppendSectionHeader("Detailed result matrix", "Fallback technical table for all recorded items.", sb);
        sb.AppendLine("<table><tr><th>Type</th><th>Name</th><th>Status</th><th>Duration</th><th>Worker / Iteration</th><th>Details</th></tr>");
        foreach (var item in report.Results)
        {
            sb.AppendLine($"<tr><td>{Escape(item.Kind)}</td><td>{Escape(GetResultName(item))}</td><td class='{(item.Success ? "ok" : "fail")}'>{(item.Success ? "Pass" : "Fail")}</td><td>{Escape(FormatDuration(item.DurationMs))}</td><td>{item.WorkerId} / {item.IterationIndex}</td><td>{Escape(DescribeResult(item))}</td></tr>");
        }
        sb.AppendLine("</table></section>");
        sb.AppendLine($"<div class='small' style='text-align:right'>Generated from report.json source data. RunId: <span class='mono'>{Escape(report.RunId)}</span></div>");
        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    private static void AppendHttpPerformanceSection(TestReport report, StringBuilder sb)
    {
        if (!string.Equals(report.ModuleId, "http.performance", StringComparison.OrdinalIgnoreCase)) return;
        var groups = report.Results.OfType<EndpointResult>().GroupBy(x => string.IsNullOrWhiteSpace(x.Name) ? "(endpoint)" : x.Name).Select(g =>
        {
            var samples = g.Select(x => x.LatencyMs > 0 ? x.LatencyMs : x.DurationMs).OrderBy(x => x).ToList();
            return new
            {
                g.Key,
                Requests = g.Count(),
                Success = g.Count(x => x.Success),
                Failures = g.Count(x => !x.Success),
                Avg = samples.Count == 0 ? 0 : samples.Average(),
                Min = samples.Count == 0 ? 0 : samples.First(),
                Max = samples.Count == 0 ? 0 : samples.Last(),
                P95 = Percentile(samples, 0.95),
                P99 = Percentile(samples, 0.99),
                Message = g.FirstOrDefault(x => !x.Success)?.ErrorMessage
            };
        }).ToList();
        if (groups.Count == 0) return;
        var maxLatency = Math.Max(1d, groups.Max(x => x.Max));
        AppendSectionHeader("Endpoint performance summary", "Per-endpoint latency profile and success ratios.", sb);
        sb.AppendLine("<table><tr><th>Endpoint</th><th>Requests</th><th>Success / Fail</th><th>Min / Avg / Max</th><th>P95 / P99</th><th>Latency</th><th>Notes</th></tr>");
        foreach (var row in groups)
        {
            sb.AppendLine($"<tr><td><strong>{Escape(row.Key)}</strong></td><td>{row.Requests}</td><td class='{(row.Failures == 0 ? "ok" : "fail")}'>{row.Success} / {row.Failures}</td><td>{Escape($"{FormatDuration(row.Min)} / {FormatDuration(row.Avg)} / {FormatDuration(row.Max)}")}</td><td>{Escape($"{FormatDuration(row.P95)} / {FormatDuration(row.P99)}")}</td><td>{RenderBar(row.Avg, maxLatency, FormatDuration(row.Avg), row.Failures == 0 ? "ok" : "warn")}</td><td>{Escape(string.IsNullOrWhiteSpace(row.Message) ? "No errors recorded." : row.Message)}</td></tr>");
        }
        sb.AppendLine("</table></section>");
    }

    private static void AppendSecurityChecksSection(TestReport report, StringBuilder sb)
    {
        if (!string.Equals(report.ModuleId, "net.security", StringComparison.OrdinalIgnoreCase)) return;
        var checks = report.Results.OfType<CheckResult>().ToList();
        if (checks.Count == 0) return;
        AppendSectionHeader("Security findings", "Readable list of issues, severities, and remediation hints.", sb);
        sb.AppendLine("<table><tr><th>Check</th><th>Status</th><th>Severity</th><th>Finding</th><th>Recommendation</th></tr>");
        foreach (var check in checks.OrderByDescending(x => x.Success ? 0 : 1))
        {
            sb.AppendLine($"<tr><td><strong>{Escape(check.Name)}</strong></td><td class='{(check.Success ? "ok" : "fail")}'>{(check.Success ? "Pass" : "Fail")}</td><td>{Escape(string.IsNullOrWhiteSpace(check.Severity) ? "n/a" : check.Severity)}</td><td>{Escape(string.IsNullOrWhiteSpace(check.ErrorMessage) ? "No issues were recorded." : check.ErrorMessage)}</td><td>{Escape(ReadString(check.Metrics, "recommendation") ?? "No recommendation attached.")}</td></tr>");
        }
        sb.AppendLine("</table></section>");
    }

    private static void AppendHttpFunctionalSection(TestReport report, StringBuilder sb)
    {
        if (!string.Equals(report.ModuleId, "http.functional", StringComparison.OrdinalIgnoreCase)) return;
        var specs = ReadArray(report.ModuleSettingsSnapshot, "Endpoints").Select(x => new
        {
            Name = ReadString(x, "Name") ?? "(endpoint)",
            Method = ReadString(x, "Method") ?? "GET",
            Path = ReadString(x, "Path") ?? "/",
            Expected = ReadInt(x, "ExpectedStatusCode"),
            Headers = ReadStringList(x, "RequiredHeaders"),
            Body = ReadString(x, "BodyContains"),
            JsonChecks = ReadStringList(x, "JsonFieldEquals")
        }).ToList();
        var results = report.Results.OfType<EndpointResult>().ToLookup(x => string.IsNullOrWhiteSpace(x.Name) ? "(endpoint)" : x.Name, StringComparer.OrdinalIgnoreCase);
        if (specs.Count == 0 && !results.Any()) return;
        AppendSectionHeader("Functional endpoint checks", "Expected contract on the left, actual result on the right.", sb);
        sb.AppendLine("<table><tr><th>Endpoint</th><th>Configured checks</th><th>Expected</th><th>Actual</th><th>Latency</th><th>Result</th><th>Details</th></tr>");
        foreach (var spec in specs)
        {
            var result = results[spec.Name].FirstOrDefault();
            var checks = new List<string>();
            if (spec.Expected.HasValue) checks.Add($"HTTP {spec.Expected}");
            if (spec.Headers.Count > 0) checks.Add($"Headers: {string.Join(", ", spec.Headers)}");
            if (!string.IsNullOrWhiteSpace(spec.Body)) checks.Add($"Body contains: {spec.Body}");
            if (spec.JsonChecks.Count > 0) checks.Add($"JSON: {string.Join(", ", spec.JsonChecks)}");
            sb.AppendLine($"<tr><td><strong>{Escape(spec.Name)}</strong><div class='small'>{Escape($"{spec.Method} {spec.Path}")}</div></td><td>{Escape(checks.Count == 0 ? "HTTP response only" : string.Join(" | ", checks))}</td><td>{Escape(spec.Expected.HasValue ? $"HTTP {spec.Expected}" : "HTTP any success")}</td><td>{Escape(result == null ? "No result" : $"HTTP {result.StatusCode?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}")}</td><td>{Escape(result == null ? "n/a" : FormatDuration(result.LatencyMs > 0 ? result.LatencyMs : result.DurationMs))}</td><td class='{(result?.Success == true ? "ok" : "fail")}'>{(result == null ? "Missing" : result.Success ? "Pass" : "Fail")}</td><td>{Escape(result == null ? "This configured endpoint was not present in the run results." : string.IsNullOrWhiteSpace(result.ErrorMessage) ? "Configured checks passed for this endpoint." : result.ErrorMessage)}</td></tr>");
        }
        sb.AppendLine("</table></section>");
    }

    private static void AppendAssetsSection(TestReport report, StringBuilder sb)
    {
        if (!string.Equals(report.ModuleId, "http.assets", StringComparison.OrdinalIgnoreCase)) return;
        var specs = ReadArray(report.ModuleSettingsSnapshot, "Assets").Select(x => new
        {
            Name = ReadString(x, "Name"),
            Url = ReadString(x, "Url") ?? string.Empty,
            ExpectedType = ReadString(x, "ExpectedContentType"),
            MaxKb = ReadInt(x, "MaxSizeKb"),
            MaxMs = ReadInt(x, "MaxLatencyMs")
        }).ToList();
        var assets = report.Results.OfType<AssetResult>().ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        if (specs.Count == 0 && assets.Count == 0) return;
        var maxLatency = Math.Max(1d, report.Results.OfType<AssetResult>().Select(x => x.LatencyMs).DefaultIfEmpty(1d).Max());
        AppendSectionHeader("Asset checks", "Expected size, type, and latency limits compared with the observed response.", sb);
        sb.AppendLine("<table><tr><th>Asset</th><th>Expected content type</th><th>Actual content type</th><th>Max KB / Actual KB</th><th>Max ms / Actual ms</th><th>Status</th><th>Notes</th></tr>");
        foreach (var spec in specs)
        {
            var key = string.IsNullOrWhiteSpace(spec.Name) ? spec.Url : spec.Name!;
            assets.TryGetValue(key, out var asset);
            var actualKb = asset == null ? "n/a" : FormatKilobytes(asset.Bytes);
            var actualMs = asset == null ? "n/a" : FormatDuration(asset.LatencyMs);
            var note = asset == null ? "No asset result was recorded for this configured entry." : string.IsNullOrWhiteSpace(asset.ErrorMessage) ? "Use the limits columns to judge contract compliance." : asset.ErrorMessage;
            sb.AppendLine($"<tr><td><strong>{Escape(key)}</strong><div class='small'>{Escape(spec.Url)}</div></td><td>{Escape(spec.ExpectedType ?? "n/a")}</td><td>{Escape(asset?.ContentType ?? "n/a")}</td><td>{Escape($"{(spec.MaxKb.HasValue ? spec.MaxKb + " KB" : "n/a")} / {actualKb}")}</td><td>{(asset == null ? "n/a" : RenderBar(asset.LatencyMs, Math.Max(maxLatency, spec.MaxMs ?? 0), $"{(spec.MaxMs.HasValue ? spec.MaxMs + " ms" : "n/a")} / {actualMs}", spec.MaxMs.HasValue && asset.LatencyMs > spec.MaxMs ? "fail" : "warn"))}</td><td class='{(asset?.Success == true ? "ok" : "fail")}'>{(asset == null ? "Missing" : asset.Success ? "Pass" : "Fail")}</td><td>{Escape(note)}</td></tr>");
        }
        sb.AppendLine("</table></section>");
    }
    private static void AppendDiagnosticsSection(TestReport report, StringBuilder sb)
    {
        if (!string.Equals(report.ModuleId, "net.diagnostics", StringComparison.OrdinalIgnoreCase)) return;
        var parsed = report.Results.OfType<CheckResult>().Select(x => new
        {
            Check = x,
            Stage = x.Name.StartsWith("DNS", StringComparison.OrdinalIgnoreCase) ? "DNS" : x.Name.StartsWith("TCP", StringComparison.OrdinalIgnoreCase) ? "TCP" : x.Name.StartsWith("TLS", StringComparison.OrdinalIgnoreCase) ? "TLS" : "Check",
            Port = ReadInt(x.Metrics, "port") ?? ParsePort(x.Name),
            ResolvedIps = string.Join(", ", ReadArray(x.Metrics, "resolvedIps").Select(y => y.GetString()).Where(y => !string.IsNullOrWhiteSpace(y))),
            Latency = ReadDouble(x.Metrics, "latencyMs") ?? x.DurationMs
        }).ToList();
        if (parsed.Count == 0) return;
        AppendSectionHeader("Network diagnostics", "Structured view of DNS, TCP, and TLS checks.", sb);
        sb.AppendLine("<div class='diag'>");
        var dnsChecks = parsed.Where(x => x.Stage == "DNS").ToList();
        if (dnsChecks.Count == 0) sb.AppendLine("<div class='card'>DNS check was not recorded.</div>");
        else foreach (var check in dnsChecks) sb.AppendLine($"<div class='card'><h3>{Escape(check.Check.Name)}</h3><div class='{(check.Check.Success ? "ok" : "fail")}'>{(check.Check.Success ? "Pass" : "Fail")}</div><div class='small'>{Escape(string.IsNullOrWhiteSpace(check.ResolvedIps) ? "No IP list recorded." : "Resolved IPs: " + check.ResolvedIps)}</div><div class='small'>Latency: {Escape(FormatDuration(check.Latency))}</div><div>{Escape(string.IsNullOrWhiteSpace(check.Check.ErrorMessage) ? "Completed." : check.Check.ErrorMessage)}</div></div>");
        sb.AppendLine("</div>");
        var ports = parsed.Where(x => x.Port.HasValue).Select(x => x.Port!.Value).Distinct().OrderBy(x => x).ToList();
        if (ports.Count > 0)
        {
            sb.AppendLine("<table><tr><th>Port</th><th>TCP connect</th><th>TLS handshake</th></tr>");
            foreach (var port in ports)
            {
                var tcp = parsed.FirstOrDefault(x => x.Stage == "TCP" && x.Port == port);
                var tls = parsed.FirstOrDefault(x => x.Stage == "TLS" && x.Port == port);
                sb.AppendLine($"<tr><td><strong>{port}</strong></td><td>{RenderDiagnosticCell(tcp)}</td><td>{RenderDiagnosticCell(tls)}</td></tr>");
            }
            sb.AppendLine("</table>");
        }
        sb.AppendLine("</section>");
    }

    private static void AppendAvailabilitySection(TestReport report, StringBuilder sb)
    {
        if (!string.Equals(report.ModuleId, "net.availability", StringComparison.OrdinalIgnoreCase)) return;
        var rows = report.Results.OfType<CheckResult>().ToList();
        if (rows.Count == 0) return;
        var maxLatency = Math.Max(1d, rows.Select(x => ReadDouble(x.Metrics, "latencyMs") ?? x.DurationMs).Max());
        AppendSectionHeader("Availability probe", "Simple service availability view for the checked target.", sb);
        sb.AppendLine("<table><tr><th>Check</th><th>Target</th><th>Status</th><th>Latency</th><th>Notes</th></tr>");
        foreach (var row in rows)
        {
            var target = ReadString(row.Metrics, "endpoint") ?? (ReadString(row.Metrics, "host") is { } host ? $"{host}:{ReadInt(row.Metrics, "port")?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}" : "n/a");
            var latency = ReadDouble(row.Metrics, "latencyMs") ?? row.DurationMs;
            sb.AppendLine($"<tr><td><strong>{Escape(row.Name)}</strong></td><td>{Escape(target)}</td><td class='{(row.Success ? "ok" : "fail")}'>{(row.Success ? "Reachable" : "Unavailable")}</td><td>{RenderBar(latency, maxLatency, FormatDuration(latency), row.Success ? "ok" : "fail")}</td><td>{Escape(string.IsNullOrWhiteSpace(row.ErrorMessage) ? "Probe completed successfully." : row.ErrorMessage)}</td></tr>");
        }
        sb.AppendLine("</table></section>");
    }

    private static void AppendPreflightSection(TestReport report, StringBuilder sb)
    {
        if (!string.Equals(report.ModuleId, "net.preflight", StringComparison.OrdinalIgnoreCase)) return;
        var checks = report.Results.OfType<PreflightResult>().ToList();
        if (checks.Count == 0) return;
        AppendSectionHeader("Preflight checklist", "Environment and connectivity checks before the main workload.", sb);
        sb.AppendLine("<div class='diag'>");
        foreach (var check in checks)
        {
            var details = new List<string>();
            if (ReadString(check.Metrics, "path") is { } path) details.Add("Path: " + path);
            if (ReadString(check.Metrics, "database") is { } db) details.Add("Database: " + db);
            if (ReadString(check.Metrics, "browsersPath") is { } bp) details.Add("Browsers path: " + bp);
            if (ReadBool(check.Metrics, "installed") is { } installed) details.Add("Installed: " + FormatBool(installed));
            if (ReadString(check.Metrics, "host") is { } host) details.Add("Target: " + host + (ReadInt(check.Metrics, "port") is { } port ? ":" + port : string.Empty));
            if (ReadString(check.Metrics, "endpoint") is { } endpoint) details.Add("Endpoint: " + endpoint);
            if (ReadInt(check.Metrics, "statusCode") is { } code) details.Add("HTTP status: " + code);
            if (!string.IsNullOrWhiteSpace(check.Details)) details.Add(check.Details!);
            sb.AppendLine($"<div class='card'><h3>{Escape(check.Name)}</h3><div class='{(check.Success ? "ok" : "fail")}'>{(check.Success ? "Passed" : "Failed")}</div><ul>{string.Concat(details.Select(x => $"<li>{Escape(x)}</li>"))}</ul>{(ReadDouble(check.Metrics, "latencyMs") is { } latency ? $"<div class='small'>Latency: {Escape(FormatDuration(latency))}</div>" : string.Empty)}</div>");
        }
        sb.AppendLine("</div></section>");
    }

    private static void AppendSnapshotSection(TestReport report, StringBuilder sb)
    {
        if (!string.Equals(report.ModuleId, "ui.snapshot", StringComparison.OrdinalIgnoreCase)) return;
        var items = report.Results.OfType<RunResult>().Where(x => !string.IsNullOrWhiteSpace(x.ScreenshotPath)).ToList();
        if (items.Count == 0) return;
        AppendSectionHeader("Screenshot gallery", "Visual artifacts are embedded directly in this report.", sb);
        sb.AppendLine("<div class='gallery'>");
        foreach (var item in items)
        {
            var path = NormalizeArtifactPath(item.ScreenshotPath) ?? item.ScreenshotPath!;
            var subtitle = "Screenshot captured during this run.";
            var pills = new List<string>();
            if (!string.IsNullOrWhiteSpace(item.DetailsJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(item.DetailsJson);
                    var root = doc.RootElement;
                    if (ReadString(root, "url") is { } url) subtitle = url;
                    if (ReadBool(root, "hasSelector") == true) pills.Add("Selector found: " + FormatBool(ReadBool(root, "selectorFound") ?? false));
                    if (ReadBool(root, "fullPage") is { } fullPage) pills.Add(fullPage ? "Full page" : "Viewport only");
                    if (ReadString(root, "waitUntil") is { } waitUntil) pills.Add(waitUntil);
                    if (ReadDouble(root, "elapsedMs") is { } elapsed) pills.Add(FormatDuration(elapsed));
                    if (TryGetProperty(root, "viewport", out var viewport)) pills.Add($"{ReadInt(viewport, "width")}x{ReadInt(viewport, "height")}");
                }
                catch
                {
                    subtitle = Trim(item.DetailsJson);
                }
            }
            sb.AppendLine($"<div class='shot'><a href='{Escape(path)}'><img loading='lazy' src='{Escape(path)}' alt='{Escape(item.Name)}'></a><div class='body'><strong>{Escape(item.Name)}</strong><div class='small'>{Escape(subtitle)}</div>{string.Concat(pills.Select(x => $"<span class='pill'>{Escape(x)}</span>"))}</div></div>");
        }
        sb.AppendLine("</div></section>");
    }

    private static void AppendTimingSection(TestReport report, StringBuilder sb)
    {
        if (!string.Equals(report.ModuleId, "ui.timing", StringComparison.OrdinalIgnoreCase)) return;
        var rows = report.Results.OfType<TimingResult>().Select(x =>
        {
            var url = x.Url ?? string.Empty;
            var profile = "n/a";
            var navigation = 0d;
            var dom = 0d;
            var load = 0d;
            var screenshot = string.Empty;
            if (!string.IsNullOrWhiteSpace(x.DetailsJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(x.DetailsJson);
                    var root = doc.RootElement;
                    url = ReadString(root, "url") ?? url;
                    if (TryGetProperty(root, "metrics", out var metrics))
                    {
                        navigation = ReadDouble(metrics, "navigationMs") ?? 0;
                        dom = ReadDouble(metrics, "domContentLoadedMs") ?? 0;
                        load = ReadDouble(metrics, "loadEventMs") ?? 0;
                    }
                    if (TryGetProperty(root, "profile", out var profileElement)) profile = $"{ReadString(profileElement, "browser") ?? "browser"} · {(TryGetProperty(profileElement, "viewport", out var viewport) ? $"{ReadInt(viewport, "width")}x{ReadInt(viewport, "height")}" : "n/a")}";
                    screenshot = NormalizeArtifactPath(ReadString(root, "screenshot")) ?? string.Empty;
                }
                catch { }
            }
            return new { x.Name, Url = url, Profile = profile, Navigation = navigation, Dom = dom, Load = load, x.Success, x.ErrorMessage, Screenshot = screenshot };
        }).ToList();
        if (rows.Count == 0) return;
        var maxMetric = Math.Max(1d, rows.SelectMany(x => new[] { x.Navigation, x.Dom, x.Load }).Max());
        AppendSectionHeader("Timing comparison", "Readable target-by-target comparison for compatibility timing runs.", sb);
        sb.AppendLine("<table><tr><th>Target</th><th>Profile</th><th>Navigation</th><th>DOMContentLoaded</th><th>Load event</th><th>Status</th><th>Artifacts</th></tr>");
        foreach (var row in rows) sb.AppendLine($"<tr><td><strong>{Escape(row.Name)}</strong><div class='small'>{Escape(row.Url)}</div></td><td>{Escape(row.Profile)}</td><td>{RenderBar(row.Navigation, maxMetric, FormatDuration(row.Navigation), row.Success ? "ok" : "warn")}</td><td>{RenderBar(row.Dom, maxMetric, FormatDuration(row.Dom), "warn")}</td><td>{RenderBar(row.Load, maxMetric, FormatDuration(row.Load), "warn")}</td><td class='{(row.Success ? "ok" : "fail")}'>{(row.Success ? "Pass" : "Fail")}{(string.IsNullOrWhiteSpace(row.ErrorMessage) ? string.Empty : $"<div class='small'>{Escape(row.ErrorMessage)}</div>")}</td><td>{(string.IsNullOrWhiteSpace(row.Screenshot) ? "<span class='small'>n/a</span>" : $"<a href='{Escape(row.Screenshot)}'>Screenshot</a>")}</td></tr>");
        sb.AppendLine("</table></section>");
    }
    private static void AppendScenarioSection(TestReport report, StringBuilder sb)
    {
        if (!string.Equals(report.ModuleId, "ui.scenario", StringComparison.OrdinalIgnoreCase)) return;
        var steps = report.Results.OfType<StepResult>().ToList();
        var screenshots = report.Results.OfType<RunResult>().Where(x => !string.IsNullOrWhiteSpace(x.ScreenshotPath)).ToList();
        AppendSectionHeader("Scenario execution", "Step-level view of the scripted UI scenario.", sb);
        if (steps.Count == 0)
        {
            sb.AppendLine("<div class='empty'>No individual step results were recorded. The scenario may have failed before step-level reporting was produced.</div>");
        }
        else
        {
            sb.AppendLine("<table><tr><th>Step</th><th>Action</th><th>Target</th><th>Duration</th><th>Status</th><th>Screenshot</th></tr>");
            foreach (var step in steps.OrderBy(GetScenarioStepIndex))
            {
                var target = !string.IsNullOrWhiteSpace(step.Selector) ? step.Selector! : step.Name;
                if (!string.IsNullOrWhiteSpace(step.DetailsJson))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(step.DetailsJson);
                        target = ReadString(doc.RootElement, "input") ?? ReadString(doc.RootElement, "url") ?? target;
                    }
                    catch { }
                }
                sb.AppendLine($"<tr><td>{GetScenarioStepIndex(step)}</td><td>{Escape(step.Action ?? "n/a")}</td><td>{Escape(target)}</td><td>{Escape(FormatDuration(step.DurationMs))}</td><td class='{(step.Success ? "ok" : "fail")}'>{(step.Success ? "Pass" : "Fail")}{(string.IsNullOrWhiteSpace(step.ErrorMessage) ? string.Empty : $"<div class='small'>{Escape(step.ErrorMessage)}</div>")}</td><td>{(string.IsNullOrWhiteSpace(step.ScreenshotPath) ? "<span class='small'>n/a</span>" : $"<a href='{Escape(NormalizeArtifactPath(step.ScreenshotPath) ?? step.ScreenshotPath!)}'>Open</a>")}</td></tr>");
            }
            sb.AppendLine("</table>");
        }
        if (screenshots.Count > 0)
        {
            sb.AppendLine("<div class='gallery' style='margin-top:14px'>");
            foreach (var shot in screenshots)
            {
                var path = NormalizeArtifactPath(shot.ScreenshotPath) ?? shot.ScreenshotPath!;
                sb.AppendLine($"<div class='shot'><a href='{Escape(path)}'><img loading='lazy' src='{Escape(path)}' alt='{Escape(shot.Name)}'></a><div class='body'><strong>{Escape(shot.Name)}</strong><div class='small'>{Escape(string.IsNullOrWhiteSpace(shot.DetailsJson) ? "Scenario artifact." : Trim(shot.DetailsJson))}</div></div></div>");
            }
            sb.AppendLine("</div>");
        }
        var comparison = ExtractRegressionComparison(report);
        if (comparison != null)
        {
            sb.AppendLine("<div class='grid' style='margin-top:14px'>");
            AppendKv("Baseline run", comparison.BaselineRunId ?? "n/a", sb);
            AppendKv("Compared steps", comparison.ComparedSteps.ToString(CultureInfo.InvariantCulture), sb);
            AppendKv("Changed steps", comparison.ChangedSteps.ToString(CultureInfo.InvariantCulture), sb);
            AppendKv("New errors", comparison.NewErrors.ToString(CultureInfo.InvariantCulture), sb);
            AppendKv("Resolved errors", comparison.ResolvedErrors.ToString(CultureInfo.InvariantCulture), sb);
            AppendKv("Comment", comparison.Message, sb);
            sb.AppendLine("</div>");
        }
        sb.AppendLine("</section>");
    }

    private static void AppendSectionHeader(string title, string? note, StringBuilder sb)
    {
        sb.AppendLine($"<section class='sec'><div class='row'><h2>{Escape(title)}</h2>{(string.IsNullOrWhiteSpace(note) ? string.Empty : $"<div class='small'>{Escape(note)}</div>")}</div>");
    }

    private static void AppendKeyMetric(string key, string value, StringBuilder sb)
    {
        sb.AppendLine($"<div class='kv'><span class='k'>{Escape(key)}</span><span class='v'>{Escape(value)}</span></div>");
    }

    private static void AppendSummaryCard(string title, string value, string note, StringBuilder sb)
    {
        sb.AppendLine($"<div class='card'><div class='n'>{Escape(value)}</div><h3>{Escape(title)}</h3><div class='small'>{Escape(note)}</div></div>");
    }

    private static void AppendKv(string key, string value, StringBuilder sb)
    {
        sb.AppendLine($"<div class='kv'><span class='k'>{Escape(key)}</span><span class='v'>{Escape(value)}</span></div>");
    }

    private static string RenderBar(double value, double max, string label, string css)
    {
        return $"<div class='bar {css}'><span style='width:{ClampPercent(max <= 0 ? 0 : value * 100d / max):F1}%'></span></div><div class='cap'><span>{Escape(label)}</span></div>";
    }

    private static string RenderDiagnosticCell(dynamic? value)
    {
        if (value == null) return "<span class='small'>n/a</span>";
        return $"<div class='{(value.Check.Success ? "ok" : "fail")}'>{(value.Check.Success ? "Pass" : "Fail")}</div><div class='small'>{Escape(FormatDuration((double)value.Latency))}</div><div class='small'>{Escape(string.IsNullOrWhiteSpace((string)value.Check.ErrorMessage) ? "Completed." : (string)value.Check.ErrorMessage)}</div>";
    }

    private static List<KeyValuePair<string, string>> DescribeModuleSettings(TestReport report)
    {
        var list = new List<KeyValuePair<string, string>>();
        switch (report.ModuleId)
        {
            case "http.performance":
                list.Add(new("Base URL", ReadString(report.ModuleSettingsSnapshot, "BaseUrl") ?? "n/a"));
                list.Add(new("Configured endpoints", ReadArray(report.ModuleSettingsSnapshot, "Endpoints").Count.ToString(CultureInfo.InvariantCulture)));
                list.Add(new("Module timeout", (ReadInt(report.ModuleSettingsSnapshot, "TimeoutSeconds")?.ToString(CultureInfo.InvariantCulture) ?? "n/a") + " s"));
                break;
            case "net.security":
                list.Add(new("URL", ReadString(report.ModuleSettingsSnapshot, "Url") ?? "n/a"));
                break;
            case "http.functional":
                list.Add(new("Base URL", ReadString(report.ModuleSettingsSnapshot, "BaseUrl") ?? "n/a"));
                list.Add(new("Configured endpoints", ReadArray(report.ModuleSettingsSnapshot, "Endpoints").Count.ToString(CultureInfo.InvariantCulture)));
                break;
            case "http.assets":
                list.Add(new("Configured assets", ReadArray(report.ModuleSettingsSnapshot, "Assets").Count.ToString(CultureInfo.InvariantCulture)));
                list.Add(new("Module timeout", (ReadInt(report.ModuleSettingsSnapshot, "TimeoutSeconds")?.ToString(CultureInfo.InvariantCulture) ?? "n/a") + " s"));
                break;
            case "net.diagnostics":
                list.Add(new("Hostname", ReadString(report.ModuleSettingsSnapshot, "Hostname") ?? "n/a"));
                list.Add(new("Ports", string.Join(", ", ReadArray(report.ModuleSettingsSnapshot, "Ports").Select(x => ReadInt(x, "Port")).Where(x => x.HasValue).Select(x => x!.Value.ToString(CultureInfo.InvariantCulture)))));
                break;
            case "net.availability":
                list.Add(new("Check type", ReadString(report.ModuleSettingsSnapshot, "CheckType") ?? "n/a"));
                list.Add(new("Target", ReadString(report.ModuleSettingsSnapshot, "Url") ?? (ReadString(report.ModuleSettingsSnapshot, "Host") is { } host ? $"{host}:{ReadInt(report.ModuleSettingsSnapshot, "Port")?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}" : "n/a")));
                break;
            case "net.preflight":
                list.Add(new("Target", ReadString(report.ModuleSettingsSnapshot, "Target") ?? "n/a"));
                break;
            case "ui.snapshot":
                list.Add(new("Targets", ReadArray(report.ModuleSettingsSnapshot, "Targets").Count.ToString(CultureInfo.InvariantCulture)));
                list.Add(new("Wait until", ReadString(report.ModuleSettingsSnapshot, "WaitUntil") ?? "n/a"));
                list.Add(new("Format", ReadString(report.ModuleSettingsSnapshot, "ScreenshotFormat") ?? "n/a"));
                list.Add(new("Full page", FormatBool(ReadBool(report.ModuleSettingsSnapshot, "FullPage") ?? false)));
                break;
            case "ui.timing":
                list.Add(new("Targets", ReadArray(report.ModuleSettingsSnapshot, "Targets").Count.ToString(CultureInfo.InvariantCulture)));
                list.Add(new("Wait until", ReadString(report.ModuleSettingsSnapshot, "WaitUntil") ?? "n/a"));
                break;
            case "ui.scenario":
                list.Add(new("Target URL", ReadString(report.ModuleSettingsSnapshot, "TargetUrl") ?? "n/a"));
                list.Add(new("Steps", ReadArray(report.ModuleSettingsSnapshot, "Steps").Count.ToString(CultureInfo.InvariantCulture)));
                break;
        }
        return list.Where(x => !string.IsNullOrWhiteSpace(x.Value)).ToList();
    }

    private static IReadOnlyList<string> BuildArtifactPaths(TestReport report)
    {
        var paths = new List<string>();
        AddArtifactPath(paths, report.Artifacts.JsonPath, "report.json");
        AddArtifactPath(paths, report.Artifacts.HtmlPath, "report.html");
        AddArtifactPath(paths, report.Artifacts.LogPath, "logs/run.log");
        foreach (var screenshotPath in report.Results.Select(result => result switch { RunResult run => run.ScreenshotPath, StepResult step => step.ScreenshotPath, _ => null }).Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase)) AddArtifactPath(paths, screenshotPath, null);
        foreach (var artifact in report.ModuleArtifacts.Where(x => !string.IsNullOrWhiteSpace(x.RelativePath))) AddArtifactPath(paths, artifact.RelativePath, null);
        return paths;
    }

    private static void AddArtifactPath(List<string> paths, string? path, string? fallback)
    {
        var normalized = NormalizeArtifactPath(path, fallback);
        if (!string.IsNullOrWhiteSpace(normalized) && !paths.Contains(normalized, StringComparer.OrdinalIgnoreCase)) paths.Add(normalized);
    }

    private static string GuessArtifactLabel(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (normalized.EndsWith("report.json", StringComparison.OrdinalIgnoreCase)) return "JSON report";
        if (normalized.EndsWith("report.html", StringComparison.OrdinalIgnoreCase)) return "HTML report";
        if (normalized.Contains("logs/run.log", StringComparison.OrdinalIgnoreCase)) return "Run log";
        if (normalized.Contains("screenshots/", StringComparison.OrdinalIgnoreCase)) return "Screenshot";
        return "Artifact";
    }

    private static string? NormalizeArtifactPath(string? path, string? fallback = null)
    {
        if (string.IsNullOrWhiteSpace(path)) return fallback?.Replace('\\', '/');
        var normalized = path.Replace('\\', '/');
        if (!Path.IsPathRooted(normalized)) return normalized;
        foreach (var marker in new[] { "report.json", "report.html", "logs/run.log", "screenshots/" })
        {
            var index = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0) return normalized[index..];
        }
        return fallback?.Replace('\\', '/') ?? normalized;
    }
    private static string DescribeResult(ResultBase item) => item switch
    {
        RunResult run => string.IsNullOrWhiteSpace(run.DetailsJson) ? run.ScreenshotPath ?? "Run artifact." : Trim(run.DetailsJson),
        StepResult step => string.IsNullOrWhiteSpace(step.DetailsJson) ? $"{step.Action} {step.Selector}".Trim() : Trim(step.DetailsJson),
        TimingResult timing => string.IsNullOrWhiteSpace(timing.DetailsJson) ? timing.Url ?? "Timing result." : Trim(timing.DetailsJson),
        CheckResult check => $"{(check.StatusCode.HasValue ? "HTTP " + check.StatusCode.Value + " · " : string.Empty)}{ReadString(check.Metrics, "endpoint") ?? ReadString(check.Metrics, "host") ?? check.ErrorMessage ?? "No extra details."}",
        EndpointResult endpoint => $"HTTP {endpoint.StatusCode?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}, {FormatDuration(endpoint.LatencyMs > 0 ? endpoint.LatencyMs : endpoint.DurationMs)}",
        AssetResult asset => $"{asset.ContentType ?? "n/a"}, {FormatKilobytes(asset.Bytes)}, {FormatDuration(asset.LatencyMs)}",
        ProbeResult probe => string.IsNullOrWhiteSpace(probe.Details) ? "No details." : probe.Details!,
        PreflightResult preflight => string.IsNullOrWhiteSpace(preflight.Details) ? "Preflight check." : preflight.Details!,
        _ => item.ErrorMessage ?? "No details."
    };

    private static RegressionComparisonView? ExtractRegressionComparison(TestReport report)
    {
        var compareResult = report.Results.OfType<RunResult>().FirstOrDefault(x => string.Equals(x.Name, "Регрессионное сравнение", StringComparison.OrdinalIgnoreCase));
        if (compareResult == null || string.IsNullOrWhiteSpace(compareResult.DetailsJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(compareResult.DetailsJson);
            var root = doc.RootElement;
            return new RegressionComparisonView(ReadString(root, "baselineRunId"), ReadInt(root, "comparedSteps") ?? 0, ReadInt(root, "changedSteps") ?? 0, ReadInt(root, "newErrors") ?? 0, ReadInt(root, "resolvedErrors") ?? 0, ReadString(root, "message") ?? string.Empty);
        }
        catch
        {
            return null;
        }
    }

    private static (string Label, string CssClass) GetSeverity(int failed, int total)
    {
        if (failed <= 0) return ("Low", "b-low");
        var ratio = total <= 0 ? 1d : (double)failed / total;
        return ratio >= 0.5d ? ("High", "b-high") : ("Medium", "b-mid");
    }

    private static List<string> BuildRecommendations(TestReport report, IReadOnlyList<ResultBase> failed)
    {
        var list = new List<string>();
        if (failed.Count == 0)
        {
            list.Add("Keep this run as a baseline for future comparison and regression review.");
            if (report.ModuleId == "ui.snapshot") list.Add("Use the embedded gallery as the visual baseline reference for UI reviews.");
            return list;
        }
        if (failed.Any(x => (x.ErrorType ?? string.Empty).Contains("Timeout", StringComparison.OrdinalIgnoreCase))) list.Add("Increase timeout values or reduce network variability before repeating the run.");
        if (failed.Any(x => (x.ErrorMessage ?? string.Empty).Contains("selector", StringComparison.OrdinalIgnoreCase))) list.Add("Review selector stability and DOM changes for the affected UI checks.");
        if (report.ModuleId == "ui.timing") list.Add("Compare the slowest profiles and normalize rendering conditions before drawing compatibility conclusions.");
        if (report.ModuleId == "ui.scenario") list.Add("Repeat the scenario after fixes and compare step-level outcomes with the latest successful baseline.");
        if (report.ModuleId.StartsWith("http.", StringComparison.OrdinalIgnoreCase)) list.Add("Review endpoint contracts, response status codes, and headers for the failing HTTP checks.");
        if (report.ModuleId == "net.security") list.Add("Prioritize high-severity findings first and confirm remediation with a follow-up baseline run.");
        if (list.Count == 0) list.Add("Inspect the attached artifacts and rerun the module after correcting the failing inputs or environment.");
        return list;
    }

    private static double Percentile(IReadOnlyList<double> sorted, double percentile)
    {
        if (sorted.Count == 0) return 0;
        var index = Math.Clamp((int)Math.Ceiling(Math.Clamp(percentile, 0, 1) * sorted.Count) - 1, 0, sorted.Count - 1);
        return sorted[index];
    }

    private static string ResolveItemSeverity(string? errorType, string? errorMessage)
    {
        var source = $"{errorType} {errorMessage}";
        if (source.Contains("timeout", StringComparison.OrdinalIgnoreCase) || source.Contains("dns", StringComparison.OrdinalIgnoreCase)) return "High";
        if (source.Contains("assert", StringComparison.OrdinalIgnoreCase) || source.Contains("selector", StringComparison.OrdinalIgnoreCase)) return "Medium";
        return "Low";
    }

    private static string GetResultName(ResultBase result) => result switch
    {
        RunResult run => run.Name,
        StepResult step => step.Name,
        CheckResult check => check.Name,
        EndpointResult endpoint => endpoint.Name,
        AssetResult asset => asset.Name,
        PreflightResult preflight => preflight.Name,
        ProbeResult probe => probe.Name,
        TimingResult timing => string.IsNullOrWhiteSpace(timing.Name) ? timing.Url ?? string.Empty : timing.Name,
        _ => string.Empty
    };

    private static string FormatStatus(TestStatus status) => status switch
    {
        TestStatus.Success => "Success",
        TestStatus.Failed => "Failed",
        TestStatus.Cancelled => "Cancelled",
        _ => status.ToString()
    };

    private static string FormatDate(DateTimeOffset value) => value == default ? "n/a" : value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);
    private static string FormatDuration(double value) => value <= 0 ? "0 ms" : value >= 1000 ? $"{value / 1000d:F2} s" : $"{value:F0} ms";
    private static string FormatKilobytes(long bytes) => bytes <= 0 ? "0 KB" : $"{bytes / 1024d:F1} KB";
    private static string FormatBool(bool value) => value ? "Yes" : "No";
    private static double ClampPercent(double value) => Math.Clamp(value, 0, 100);
    private static string Trim(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Length > 220 ? value[..220] + "..." : value;
    private static string Escape(string? value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
    private static int GetScenarioStepIndex(StepResult step)
    {
        if (string.IsNullOrWhiteSpace(step.DetailsJson)) return step.ItemIndex ?? 0;
        try
        {
            using var doc = JsonDocument.Parse(step.DetailsJson);
            return ReadInt(doc.RootElement, "stepIndex") ?? step.ItemIndex ?? 0;
        }
        catch
        {
            return step.ItemIndex ?? 0;
        }
    }

    private static int? ParsePort(string name) => name.LastIndexOf(':') is var index && index >= 0 && int.TryParse(name[(index + 1)..].Trim(), out var port) ? port : null;
    private static string? ReadString(JsonElement? element, string propertyName) => element is not { } value ? null : ReadString(value, propertyName);
    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value)) return null;
        return value.ValueKind switch { JsonValueKind.String => value.GetString(), JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.ToString(), _ => null };
    }
    private static int? ReadInt(JsonElement? element, string propertyName) => element is not { } value ? null : ReadInt(value, propertyName);
    private static int? ReadInt(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number) ? number : null;
    }
    private static double? ReadDouble(JsonElement? element, string propertyName) => element is not { } value ? null : ReadDouble(value, propertyName);
    private static double? ReadDouble(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)) return number;
        return value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number) ? number : null;
    }
    private static bool? ReadBool(JsonElement? element, string propertyName) => element is not { } value ? null : ReadBool(value, propertyName);
    private static bool? ReadBool(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value)) return null;
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False) return value.GetBoolean();
        return value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed) ? parsed : null;
    }
    private static List<string> ReadStringList(JsonElement element, string propertyName) => ReadArray(element, propertyName).Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : x.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToList();
    private static List<JsonElement> ReadArray(JsonElement? element, string propertyName) => element is not { } value ? new List<JsonElement>() : ReadArray(value, propertyName);
    private static List<JsonElement> ReadArray(JsonElement element, string propertyName) => !TryGetProperty(element, propertyName, out var value) || value.ValueKind != JsonValueKind.Array ? new List<JsonElement>() : value.EnumerateArray().ToList();
    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object) return false;
        if (element.TryGetProperty(propertyName, out value)) return true;
        foreach (var property in element.EnumerateObject()) if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase)) { value = property.Value; return true; }
        return false;
    }

    private sealed record RegressionComparisonView(string? BaselineRunId, int ComparedSteps, int ChangedSteps, int NewErrors, int ResolvedErrors, string Message);
}
