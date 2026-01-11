using System.Diagnostics;
using System.Net.Sockets;

namespace WebLoadTester.Infrastructure.Network;

public static class TcpProbe
{
    public static async Task<(bool Success, double DurationMs, string? Error)> ConnectAsync(string host, int port, CancellationToken ct)
    {
        try
        {
            using var client = new TcpClient();
            var sw = Stopwatch.StartNew();
            await client.ConnectAsync(host, port, ct);
            sw.Stop();
            return (true, sw.Elapsed.TotalMilliseconds, null);
        }
        catch (Exception ex)
        {
            return (false, 0, ex.Message);
        }
    }
}
