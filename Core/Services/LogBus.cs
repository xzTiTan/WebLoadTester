using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;

namespace WebLoadTester.Core.Services;

public sealed class LogBus : ILogSink
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>();

    public void Info(string message) => Publish("INFO", message);
    public void Warn(string message) => Publish("WARN", message);
    public void Error(string message) => Publish("ERROR", message);

    private void Publish(string level, string message)
    {
        var line = $"[{DateTimeOffset.Now:HH:mm:ss}] {level} {message}";
        _channel.Writer.TryWrite(line);
    }

    public async IAsyncEnumerable<string> ReadAllAsync([EnumeratorCancellation] CancellationToken ct)
    {
        while (await _channel.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
        {
            while (_channel.Reader.TryRead(out var item))
            {
                yield return item;
            }
        }
    }
}
