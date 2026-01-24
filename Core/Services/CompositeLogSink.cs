using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;

namespace WebLoadTester.Core.Services;

/// <summary>
/// Композитный логгер для одновременной записи в несколько приёмников.
/// </summary>
public class CompositeLogSink : ILogSink
{
    private readonly IReadOnlyList<ILogSink> _sinks;

    public CompositeLogSink(IEnumerable<ILogSink> sinks)
    {
        _sinks = sinks.ToList();
    }

    public void Info(string message)
    {
        foreach (var sink in _sinks)
        {
            sink.Info(message);
        }
    }

    public void Warn(string message)
    {
        foreach (var sink in _sinks)
        {
            sink.Warn(message);
        }
    }

    public void Error(string message)
    {
        foreach (var sink in _sinks)
        {
            sink.Error(message);
        }
    }

    public Task CompleteAsync()
    {
        return Task.WhenAll(_sinks.Select(s => s.CompleteAsync()));
    }
}
