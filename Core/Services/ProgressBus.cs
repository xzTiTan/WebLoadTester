using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Services;

public sealed class ProgressBus : IProgressSink
{
    private readonly Channel<ProgressUpdate> _channel = Channel.CreateUnbounded<ProgressUpdate>();

    public void Report(ProgressUpdate update)
    {
        _channel.Writer.TryWrite(update);
    }

    public async Task<ProgressUpdate?> ReadAsync(CancellationToken ct)
    {
        if (await _channel.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
        {
            if (_channel.Reader.TryRead(out var update))
            {
                return update;
            }
        }

        return null;
    }
}
