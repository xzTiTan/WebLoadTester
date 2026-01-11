using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace WebLoadTester.Infrastructure.Network;

public sealed class TlsProbe
{
    public async Task<(bool Success, long DurationMs, int? DaysToExpiry, string? Error)> HandshakeAsync(string host, int port, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, ct);
            using var ssl = new SslStream(client.GetStream(), false);
            await ssl.AuthenticateAsClientAsync(host, cancellationToken: ct);
            var cert = new X509Certificate2(ssl.RemoteCertificate);
            var days = (int)(cert.NotAfter - DateTime.UtcNow).TotalDays;
            return (true, sw.ElapsedMilliseconds, days, null);
        }
        catch (Exception ex)
        {
            return (false, sw.ElapsedMilliseconds, null, ex.Message);
        }
    }
}
