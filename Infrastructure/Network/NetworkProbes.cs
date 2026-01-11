using System;
using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace WebLoadTester.Infrastructure.Network;

public static class NetworkProbes
{
    public static async Task<(bool success, double durationMs, string details)> DnsProbeAsync(string host, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, ct);
            sw.Stop();
            return (addresses.Length > 0, sw.Elapsed.TotalMilliseconds, string.Join(", ", addresses));
        }
        catch (Exception ex)
        {
            sw.Stop();
            return (false, sw.Elapsed.TotalMilliseconds, ex.Message);
        }
    }

    public static async Task<(bool success, double durationMs, string details)> TcpProbeAsync(string host, int port, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, ct);
            sw.Stop();
            return (true, sw.Elapsed.TotalMilliseconds, "Connected");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return (false, sw.Elapsed.TotalMilliseconds, ex.Message);
        }
    }

    public static async Task<(bool success, double durationMs, string details, int? daysToExpiry)> TlsProbeAsync(string host, int port, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, ct);
            await using var stream = new SslStream(client.GetStream(), false, (_, _, _, _) => true);
            await stream.AuthenticateAsClientAsync(host, cancellationToken: ct);
            sw.Stop();
            var cert = new X509Certificate2(stream.RemoteCertificate);
            var daysToExpiry = (int)(cert.NotAfter - DateTime.UtcNow).TotalDays;
            return (true, sw.Elapsed.TotalMilliseconds, cert.Subject, daysToExpiry);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return (false, sw.Elapsed.TotalMilliseconds, ex.Message, null);
        }
    }
}
