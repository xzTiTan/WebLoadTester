using System;
using System.Net.Http;

namespace WebLoadTester.Infrastructure.Http;

public static class HttpClientProvider
{
    public static HttpClient Create(TimeSpan timeout)
    {
        return new HttpClient
        {
            Timeout = timeout
        };
    }
}
