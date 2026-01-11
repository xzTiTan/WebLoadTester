using System;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace WebLoadTester.Infrastructure.Network;

public static class NetworkProbes
{
    public static async Task<(TimeSpan duration, IPAddress[] addresses)> DnsResolveAsync(string host, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var addresses = await Dns.GetHostAddressesAsync(host, ct).ConfigureAwait(false);
        sw.Stop();
        return (sw.Elapsed, addresses);
    }

    public static async Task<TimeSpan> TcpConnectAsync(string host, int port, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        using var client = new TcpClient();
        await client.ConnectAsync(host, port, ct).ConfigureAwait(false);
        sw.Stop();
        return sw.Elapsed;
    }

    public static async Task<(TimeSpan duration, int daysToExpiry)> TlsHandshakeAsync(string host, int port, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        using var client = new TcpClient();
        await client.ConnectAsync(host, port, ct).ConfigureAwait(false);
        using var sslStream = new SslStream(client.GetStream(), false);
        await sslStream.AuthenticateAsClientAsync(host).ConfigureAwait(false);
        sw.Stop();

        var cert = new X509Certificate2(sslStream.RemoteCertificate);
        var days = (int)(cert.NotAfter - DateTime.UtcNow).TotalDays;
        return (sw.Elapsed, days);
    }
}
