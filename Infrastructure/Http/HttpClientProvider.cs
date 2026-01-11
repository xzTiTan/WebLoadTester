namespace WebLoadTester.Infrastructure.Http;

public sealed class HttpClientProvider
{
    private readonly HttpClient _client;

    public HttpClientProvider()
    {
        _client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    public HttpClient Client => _client;
}
