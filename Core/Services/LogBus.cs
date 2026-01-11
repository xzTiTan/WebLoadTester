using System.Threading.Channels;
using WebLoadTester.Core.Contracts;

namespace WebLoadTester.Core.Services;

public sealed class LogBus : ILogSink
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>();

    public ChannelReader<string> Reader => _channel.Reader;

    public void Log(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _channel.Writer.TryWrite($"[{DateTimeOffset.Now:HH:mm:ss}] {message}");
    }
}
