using System;

namespace WebLoadTester.Presentation.ViewModels.Controls;

public class LogLineViewModel
{
    public LogLineViewModel(
        DateTimeOffset timestamp,
        string level,
        string moduleId,
        string message,
        int? workerId = null,
        int? iteration = null)
    {
        Timestamp = timestamp;
        Level = string.IsNullOrWhiteSpace(level) ? "INFO" : level.Trim().ToUpperInvariant();
        ModuleId = string.IsNullOrWhiteSpace(moduleId) ? "core" : moduleId.Trim();
        Message = message ?? string.Empty;
        WorkerId = workerId;
        Iteration = iteration;
    }

    public DateTimeOffset Timestamp { get; }
    public string Level { get; }
    public string ModuleId { get; }
    public string Message { get; }
    public int? WorkerId { get; }
    public int? Iteration { get; }

    public string RenderedText
    {
        get
        {
            var context = WorkerId.HasValue || Iteration.HasValue
                ? $" (W{WorkerId?.ToString() ?? "-"}/I{Iteration?.ToString() ?? "-"})"
                : string.Empty;
            return $"{Timestamp:HH:mm:ss.fff} [{Level}] [{ModuleId}]{context} {Message}";
        }
    }
}
