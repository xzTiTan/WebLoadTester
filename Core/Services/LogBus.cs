using System.Collections.ObjectModel;
using System.Threading.Channels;
using Avalonia.Threading;
using WebLoadTester.Core.Contracts;

namespace WebLoadTester.Core.Services;

public sealed class LogBus : ILogSink, IDisposable
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = true
    });

    private readonly CancellationTokenSource _cts = new();

    public ObservableCollection<string> Entries { get; } = new();

    public LogBus()
    {
        _ = Task.Run(() => ConsumeAsync(_cts.Token));
    }

    public void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _channel.Writer.TryWrite(line);
    }

    private async Task ConsumeAsync(CancellationToken ct)
    {
        try
        {
            while (await _channel.Reader.WaitToReadAsync(ct))
            {
                while (_channel.Reader.TryRead(out var line))
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Entries.Add(line);
                        if (Entries.Count > 3000)
                        {
                            Entries.RemoveAt(0);
                        }
                    });
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
