using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace WebLoadTester.Infrastructure.Network;

public static class TlsProbe
{
    public static async Task<(bool Success, double DurationMs, int DaysToExpiry, string? Error)> HandshakeAsync(string host, int port, CancellationToken ct)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, ct);
            using var ssl = new SslStream(client.GetStream(), false, (_, _, _, _) => true);
            var sw = Stopwatch.StartNew();
            await ssl.AuthenticateAsClientAsync(host);
            sw.Stop();
            var cert = new X509Certificate2(ssl.RemoteCertificate ?? throw new InvalidOperationException("No cert"));
            var days = (int)(cert.NotAfter - DateTimeOffset.Now).TotalDays;
            return (true, sw.Elapsed.TotalMilliseconds, days, null);
        }
        catch (Exception ex)
        {
            return (false, 0, 0, ex.Message);
        }
    }
}
