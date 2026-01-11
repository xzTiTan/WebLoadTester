namespace WebLoadTester.Infrastructure.Http;

public sealed class HttpClientProvider
{
    public HttpClient Create(TimeSpan timeout)
    {
        return new HttpClient
        {
            Timeout = timeout
        };
    }
}
