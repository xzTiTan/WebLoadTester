using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Infrastructure.Telegram;
using Xunit;

namespace WebLoadTester.Tests;

public class TelegramClientTests
{
    [Fact]
    public async Task SendMessageAsync_BuildsExpectedEndpointAndPayload()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = new TelegramClient(new HttpClient(handler));

        var result = await client.SendMessageAsync("test-token", "12345", "hello", CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal("https://api.telegram.org/bottest-token/sendMessage", handler.LastRequest!.RequestUri!.ToString());

        Assert.Contains("chat_id=12345", handler.LastRequestBody);
        Assert.Contains("text=hello", handler.LastRequestBody);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task SendMessageAsync_ReturnsFailureForHttpErrors(HttpStatusCode statusCode)
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent("telegram error")
        });
        var client = new TelegramClient(new HttpClient(handler));

        var result = await client.SendMessageAsync("token", "chat", "payload", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(((int)statusCode).ToString(), result.Error);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public HttpRequestMessage? LastRequest { get; private set; }
        public string LastRequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return _responseFactory(request);
        }
    }
}
