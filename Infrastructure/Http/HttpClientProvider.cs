using System;
using System.Net.Http;

namespace WebLoadTester.Infrastructure.Http;

public sealed class HttpClientProvider
{
    private readonly Lazy<HttpClient> _client = new(() => new HttpClient());

    public HttpClient Client => _client.Value;
}
