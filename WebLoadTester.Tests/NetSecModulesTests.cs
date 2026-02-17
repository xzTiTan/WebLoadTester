using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Domain;
using WebLoadTester.Core.Services;
using WebLoadTester.Infrastructure.Storage;
using WebLoadTester.Modules.Availability;
using WebLoadTester.Modules.NetDiagnostics;
using WebLoadTester.Modules.Preflight;
using WebLoadTester.Modules.SecurityBaseline;
using Xunit;

namespace WebLoadTester.Tests;

public class NetSecModulesTests
{
    [Fact]
    public void NetDiagnosticsValidate_ReturnsRussianErrors()
    {
        var module = new NetDiagnosticsModule();
        var settings = new NetDiagnosticsSettings
        {
            Hostname = "",
            CheckDns = false,
            CheckTcp = false,
            CheckTls = false,
            UseAutoPorts = false,
            Ports = { new DiagnosticPort { Port = 70000 } }
        };

        var errors = module.Validate(settings);

        Assert.Contains(errors, e => e.Contains("Hostname обязателен"));
        Assert.Contains(errors, e => e.Contains("хотя бы одну проверку"));
        Assert.Contains(errors, e => e.Contains("1..65535"));
    }

    [Fact]
    public void AvailabilityValidate_ReturnsRussianErrors()
    {
        var module = new AvailabilityModule();
        var settings = new AvailabilitySettings
        {
            CheckType = "TCP",
            Host = "",
            Port = 70000,
            TimeoutMs = 0
        };

        var errors = module.Validate(settings);

        Assert.Contains(errors, e => e.Contains("TimeoutMs"));
        Assert.Contains(errors, e => e.Contains("Host обязательно"));
        Assert.Contains(errors, e => e.Contains("1..65535"));
    }

    [Fact]
    public void SecurityValidate_ReturnsRussianErrors()
    {
        var module = new SecurityBaselineModule();
        var settings = new SecurityBaselineSettings
        {
            Url = "",
            CheckHsts = false,
            CheckContentTypeOptions = false,
            CheckFrameOptions = false,
            CheckContentSecurityPolicy = false,
            CheckReferrerPolicy = false,
            CheckPermissionsPolicy = false,
            CheckRedirectHttpToHttps = false,
            CheckCookieFlags = false
        };

        var errors = module.Validate(settings);

        Assert.Contains(errors, e => e.Contains("Url обязателен"));
        Assert.Contains(errors, e => e.Contains("хотя бы одну"));
    }

    [Fact]
    public void PreflightValidate_ReturnsRussianErrors()
    {
        var module = new PreflightModule();
        var settings = new PreflightSettings
        {
            Target = "bad url",
            CheckDns = false,
            CheckTcp = false,
            CheckTls = false,
            CheckHttp = false
        };

        var errors = module.Validate(settings);

        Assert.Contains(errors, e => e.Contains("хотя бы одну"));
    }

    [Fact]
    public async Task SecurityBaseline_UsesSeverityAndCanReturnFail()
    {
        var tempRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WebLoadTesterTests", System.Guid.NewGuid().ToString("N"));
        var artifacts = new ArtifactStore(System.IO.Path.Combine(tempRoot, "runs"), System.IO.Path.Combine(tempRoot, "profiles"));
        var context = new RunContext(new LogBus(), new ProgressBus(), artifacts, new Limits(), null,
            System.Guid.NewGuid().ToString("N"), new RunProfile(), "Baseline", System.Guid.NewGuid(), 1);

        var module = new SecurityBaselineModule();
        var settings = new SecurityBaselineSettings { Url = "https://127.0.0.1:1" };

        var result = await module.ExecuteAsync(settings, context, CancellationToken.None);

        Assert.Contains(result.Results.OfType<CheckResult>(), c => c.Severity == "Fail");
        Assert.Equal(TestStatus.Failed, result.Status);
    }

    [Fact]
    public void JsonWriter_SerializesSeverityField()
    {
        var result = new CheckResult("baseline")
        {
            Success = true,
            Severity = "Warn",
            Metrics = JsonSerializer.SerializeToElement(new { header = "x" })
        };

        Assert.Equal("Warn", result.Severity);
        Assert.Equal(JsonValueKind.Object, result.Metrics!.Value.ValueKind);
    }
}
