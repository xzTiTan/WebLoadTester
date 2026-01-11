using System;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;

namespace WebLoadTester.Core.Services;

public class LogBus : ILogSink
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
    {
        SingleReader = false,
        SingleWriter = false,
        AllowSynchronousContinuations = true
    });

    public IAsyncEnumerable<string> ReadAllAsync() => _channel.Reader.ReadAllAsync();

    public void Info(string message) => Write("INFO", message);

    public void Warn(string message) => Write("WARN", message);

    public void Error(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        var line = $"[{DateTimeOffset.Now:HH:mm:ss}] {level}: {message}";
        _channel.Writer.TryWrite(line);
    }

    public Task CompleteAsync()
    {
        _channel.Writer.TryComplete();
        return Task.CompletedTask;
    }
}
