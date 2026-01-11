using System.Diagnostics;
using System.Net.Sockets;

namespace WebLoadTester.Infrastructure.Network;

public sealed class TcpProbe
{
    public async Task<(bool Success, long DurationMs, string? Error)> ConnectAsync(string host, int port, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, ct);
            return (true, sw.ElapsedMilliseconds, null);
        }
        catch (Exception ex)
        {
            return (false, sw.ElapsedMilliseconds, ex.Message);
        }
    }
}
