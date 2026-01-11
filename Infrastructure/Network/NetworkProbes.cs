using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace WebLoadTester.Infrastructure.Network;

/// <summary>
/// Набор сетевых зондов: DNS, TCP и TLS.
/// </summary>
public static class NetworkProbes
{
    /// <summary>
    /// Проверяет DNS-разрешение хоста.
    /// </summary>
    public static async Task<(bool success, double durationMs, string details)> DnsProbeAsync(string host, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, ct);
            sw.Stop();
            var detail = string.Join(", ", addresses.Select(address => address.ToString()));
            return (addresses.Length > 0, sw.Elapsed.TotalMilliseconds, detail);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return (false, sw.Elapsed.TotalMilliseconds, ex.Message);
        }
    }

    /// <summary>
    /// Проверяет TCP-подключение к хосту и порту.
    /// </summary>
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

    /// <summary>
    /// Проверяет TLS-соединение и срок действия сертификата.
    /// </summary>
    public static async Task<(bool success, double durationMs, string details, int? daysToExpiry)> TlsProbeAsync(string host, int port, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, ct);
            await using var stream = new SslStream(client.GetStream(), false, (_, _, _, _) => true);
            await stream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = host
            }, ct);
            sw.Stop();
            if (stream.RemoteCertificate == null)
            {
                return (false, sw.Elapsed.TotalMilliseconds, "No certificate", null);
            }

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
