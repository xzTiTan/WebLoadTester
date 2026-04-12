using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Core.Services;
using WebLoadTester.Core.Services.ReportWriters;
using WebLoadTester.Infrastructure.Playwright;
using WebLoadTester.Infrastructure.Storage;
using WebLoadTester.Modules.Preflight;
using WebLoadTester.Presentation.ViewModels;

namespace WebLoadTester;

internal static class SmokeReportRunner
{
    public static async Task<int> RunAsync()
    {
        CompositeLogSink? log = null;
        try
        {
            var settingsService = new AppSettingsService();
            PlaywrightFactory.ConfigureBrowsersPath(settingsService.Settings.BrowsersDirectory);
            var artifactStore = new ArtifactStore(
                settingsService.Settings.RunsDirectory,
                Path.Combine(settingsService.Settings.DataDirectory, "profiles"));
            var runStore = new SqliteRunStore(settingsService.Settings.DatabasePath);
            await runStore.InitializeAsync(CancellationToken.None);

            var runId = Guid.NewGuid().ToString("N");
            var runFolder = Path.Combine(settingsService.Settings.RunsDirectory, runId);
            var profile = new RunProfile
            {
                Name = "SmokeReport",
                Mode = RunMode.Iterations,
                Iterations = 1,
                Parallelism = 1,
                TimeoutSeconds = 30,
                PauseBetweenIterationsMs = 0,
                Headless = true,
                ScreenshotsPolicy = ScreenshotsPolicy.Off,
                HtmlReportEnabled = true,
                TelegramEnabled = false,
                PreflightEnabled = false
            };

            var progress = new ProgressBus();
            progress.ProgressChanged += update => Console.WriteLine($"PROGRESS current={update.Current} total={update.Total} message={update.Message}");

            log = new CompositeLogSink(new ILogSink[]
            {
                new ConsoleLogSink(),
                new FileLogSink(artifactStore.GetLogPath(runId))
            });

            var context = new RunContext(
                log,
                progress,
                artifactStore,
                new Limits(),
                telegram: null,
                runId,
                profile,
                testName: "smoke_report_preflight",
                testCaseId: Guid.NewGuid(),
                testCaseVersion: 1);

            var orchestrator = new RunOrchestrator(
                new JsonReportWriter(artifactStore),
                new HtmlReportWriter(artifactStore),
                runStore);

            var module = new SmokePreflightModule();
            var moduleSettings = new PreflightSettings
            {
                Target = "https://smoke.local/report",
                CheckDns = true,
                CheckTcp = true,
                CheckTls = true,
                CheckHttp = true
            };

            var report = await orchestrator.StartAsync(module, moduleSettings, context, CancellationToken.None);
            await log.CompleteAsync();
            log = null;

            var jsonPath = Path.Combine(runFolder, "report.json");
            var htmlPath = Path.Combine(runFolder, "report.html");
            var logPath = Path.Combine(runFolder, "logs", "run.log");

            var jsonExists = File.Exists(jsonPath);
            var htmlExists = File.Exists(htmlPath);
            var logExists = File.Exists(logPath);
            var folderExists = Directory.Exists(runFolder);

            var detail = await runStore.GetRunDetailAsync(runId, CancellationToken.None);
            var historyHasJson = detail?.Artifacts.Any(a => a.ArtifactType == "JsonReport" && a.RelativePath == "report.json") == true;
            var historyHasHtml = detail?.Artifacts.Any(a => a.ArtifactType == "HtmlReport" && a.RelativePath == "report.html") == true;
            var historyHasLog = detail?.Artifacts.Any(a => a.ArtifactType == "Log" && a.RelativePath == "logs/run.log") == true;

            var repeatParseOk = false;
            var repeatParseError = string.Empty;
            if (jsonExists)
            {
                var reportJson = await File.ReadAllTextAsync(jsonPath);
                repeatParseOk = RunsTabViewModel.TryParseRepeatSnapshot(reportJson, out _, out repeatParseError);
            }

            Console.WriteLine($"RunId={runId}");
            Console.WriteLine($"RunFolder={runFolder}");
            Console.WriteLine($"JsonExists={jsonExists}");
            Console.WriteLine($"HtmlExists={htmlExists}");
            Console.WriteLine($"LogExists={logExists}");
            Console.WriteLine($"HistoryHasJson={historyHasJson}");
            Console.WriteLine($"HistoryHasHtml={historyHasHtml}");
            Console.WriteLine($"HistoryHasLog={historyHasLog}");
            Console.WriteLine($"RepeatParseOk={repeatParseOk}");
            if (!repeatParseOk && !string.IsNullOrWhiteSpace(repeatParseError))
            {
                Console.WriteLine($"RepeatParseError={repeatParseError}");
            }

            var success =
                report.Status == TestStatus.Success &&
                folderExists &&
                jsonExists &&
                htmlExists &&
                logExists &&
                string.Equals(NormalizePath(report.Artifacts.JsonPath), "report.json", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(NormalizePath(report.Artifacts.HtmlPath), "report.html", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(NormalizePath(report.Artifacts.LogPath), "logs/run.log", StringComparison.OrdinalIgnoreCase) &&
                historyHasJson &&
                historyHasHtml &&
                historyHasLog &&
                repeatParseOk;

            Console.WriteLine(success ? "SmokeResult=SUCCESS" : "SmokeResult=FAIL");
            return success ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"SmokeResult=FAIL Exception={ex}");
            return 1;
        }
        finally
        {
            if (log != null)
            {
                await log.CompleteAsync();
            }
        }
    }

    private sealed class SmokePreflightModule : ITestModule
    {
        public string Id => "net.preflight";
        public string DisplayName => "Smoke Preflight";
        public string Description => "Technical smoke report generation path.";
        public TestFamily Family => TestFamily.NetSec;
        public Type SettingsType => typeof(PreflightSettings);

        public object CreateDefaultSettings() => new PreflightSettings();

        public IReadOnlyList<string> Validate(object settings)
        {
            if (settings is not PreflightSettings preflight || string.IsNullOrWhiteSpace(preflight.Target))
            {
                return new[] { "Smoke settings target is required." };
            }

            return Array.Empty<string>();
        }

        public Task<ModuleResult> ExecuteAsync(object settings, IRunContext ctx, CancellationToken ct)
        {
            var target = settings is PreflightSettings preflight ? preflight.Target : "unknown";
            ctx.Log.Info($"Smoke module executed for target: {target}");

            return Task.FromResult(new ModuleResult
            {
                Status = TestStatus.Success,
                Results = new List<ResultBase>
                {
                    new CheckResult("Smoke report generation")
                    {
                        Success = true,
                        DurationMs = 1,
                        StatusCode = 200,
                        Severity = "Info",
                        ErrorMessage = null,
                        Metrics = JsonSerializer.SerializeToElement(new
                        {
                            smoke = true,
                            target
                        })
                    }
                }
            });
        }
    }

    private sealed class ConsoleLogSink : ILogSink
    {
        public void Info(string message) => Console.WriteLine($"INFO {message}");
        public void Warn(string message) => Console.WriteLine($"WARN {message}");
        public void Error(string message) => Console.WriteLine($"ERROR {message}");
        public Task CompleteAsync() => Task.CompletedTask;
    }

    private static string NormalizePath(string? path) => (path ?? string.Empty).Replace('\\', '/');
}
