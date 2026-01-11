using System;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;

namespace WebLoadTester.Core.Services;

/// <summary>
/// Шина логов на основе канала для асинхронного чтения.
/// </summary>
public class LogBus : ILogSink
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
    {
        SingleReader = false,
        SingleWriter = false,
        AllowSynchronousContinuations = true
    });

    /// <summary>
    /// Асинхронно читает все строки логов.
    /// </summary>
    public IAsyncEnumerable<string> ReadAllAsync() => _channel.Reader.ReadAllAsync();

    /// <summary>
    /// Пишет информационный лог.
    /// </summary>
    public void Info(string message) => Write("INFO", message);

    /// <summary>
    /// Пишет предупреждение.
    /// </summary>
    public void Warn(string message) => Write("WARN", message);

    /// <summary>
    /// Пишет ошибку.
    /// </summary>
    public void Error(string message) => Write("ERROR", message);

    /// <summary>
    /// Форматирует и отправляет строку лога в канал.
    /// </summary>
    private void Write(string level, string message)
    {
        var line = $"[{DateTimeOffset.Now:HH:mm:ss}] {level}: {message}";
        _channel.Writer.TryWrite(line);
    }

    /// <summary>
    /// Завершает канал логов.
    /// </summary>
    public Task CompleteAsync()
    {
        _channel.Writer.TryComplete();
        return Task.CompletedTask;
    }
}
