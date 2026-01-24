using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;

namespace WebLoadTester.Infrastructure.Storage;

/// <summary>
/// Логгер, пишущий строки в файл.
/// </summary>
public sealed class FileLogSink : ILogSink, IDisposable
{
    private readonly StreamWriter _writer;

    public FileLogSink(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(filePath, append: true, encoding: Encoding.UTF8)
        {
            AutoFlush = true
        };
    }

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message) => Write("ERROR", message);

    public Task CompleteAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    private void Write(string level, string message)
    {
        var line = $"[{DateTimeOffset.Now:O}] {level}: {message}";
        _writer.WriteLine(line);
    }

    public void Dispose()
    {
        _writer.Dispose();
    }
}
