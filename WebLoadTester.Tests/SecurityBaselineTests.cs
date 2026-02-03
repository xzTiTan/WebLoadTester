using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Domain;
using WebLoadTester.Core.Services;
using WebLoadTester.Infrastructure.Storage;
using WebLoadTester.Modules.SecurityBaseline;
using Xunit;

namespace WebLoadTester.Tests;

public class SecurityBaselineTests
{
    [Fact]
    public async Task ReportsConfiguredHeaders()
    {
        var port = GetFreePort();
        var prefix = $"http://localhost:{port}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();

        var serverTask = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync();
            context.Response.StatusCode = 200;
            context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Add("X-Frame-Options", "DENY");
            await context.Response.OutputStream.FlushAsync();
            context.Response.Close();
        });

        var tempRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WebLoadTesterTests", Guid.NewGuid().ToString("N"));
        var artifacts = new ArtifactStore(System.IO.Path.Combine(tempRoot, "runs"), System.IO.Path.Combine(tempRoot, "profiles"));
        var context = new RunContext(new LogBus(), new ProgressBus(), artifacts, new Limits(), null,
            Guid.NewGuid().ToString("N"), new RunProfile(), "Baseline", Guid.NewGuid(), 1);

        var module = new SecurityBaselineModule();
        var settings = new SecurityBaselineSettings
        {
            Url = prefix,
            CheckRedirectHttpToHttps = false
        };

        var result = await module.ExecuteAsync(settings, context, CancellationToken.None);
        await serverTask;

        var contentTypeResult = result.Results.OfType<WebLoadTester.Core.Domain.CheckResult>()
            .FirstOrDefault(r => r.Name == "X-Content-Type-Options");

        Assert.NotNull(contentTypeResult);
        Assert.True(contentTypeResult!.Success);
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
