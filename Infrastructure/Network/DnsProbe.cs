using System.Diagnostics;

namespace WebLoadTester.Infrastructure.Network;

public sealed class DnsProbe
{
    public async Task<(bool Success, long DurationMs, string? Error)> ResolveAsync(string host, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            _ = await Dns.GetHostAddressesAsync(host, ct);
            return (true, sw.ElapsedMilliseconds, null);
        }
        catch (Exception ex)
        {
            return (false, sw.ElapsedMilliseconds, ex.Message);
        }
    }
}
