using System.Diagnostics;
using System.Net;

namespace WebLoadTester.Infrastructure.Network;

public static class DnsProbe
{
    public static async Task<(bool Success, double DurationMs, string? Error)> ResolveAsync(string host, CancellationToken ct)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            _ = await Dns.GetHostAddressesAsync(host, ct);
            sw.Stop();
            return (true, sw.Elapsed.TotalMilliseconds, null);
        }
        catch (Exception ex)
        {
            return (false, 0, ex.Message);
        }
    }
}
