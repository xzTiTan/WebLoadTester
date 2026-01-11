using System;
using System.Net.Http;

namespace WebLoadTester.Infrastructure.Http;

/// <summary>
/// Фабрика HTTP-клиентов с заданным таймаутом.
/// </summary>
public static class HttpClientProvider
{
    /// <summary>
    /// Создаёт HttpClient с указанным таймаутом.
    /// </summary>
    public static HttpClient Create(TimeSpan timeout)
    {
        return new HttpClient
        {
            Timeout = timeout
        };
    }
}
