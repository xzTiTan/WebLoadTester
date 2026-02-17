using System;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Infrastructure.Telegram;

public enum TelegramIndicatorState
{
    Off,
    Ok,
    Error
}

public sealed record TelegramRuntimeStatus(
    TelegramIndicatorState State,
    string? LastError,
    DateTimeOffset? LastSentAtUtc,
    bool IsConfigured);

public interface ITelegramRunNotifier
{
    TelegramRuntimeStatus Status { get; }
    event EventHandler<TelegramRuntimeStatus>? StatusChanged;

    Task<TelegramSendResult> NotifyStartAsync(TestRunNotificationContext context, bool telegramNotify, CancellationToken ct);
    Task<TelegramSendResult> NotifyProgressAsync(TestRunNotificationContext context, ProgressUpdate update, bool telegramNotify, CancellationToken ct);
    Task<TelegramSendResult> NotifyCompletionAsync(TestReport report, bool telegramNotify, CancellationToken ct);
    Task<TelegramSendResult> NotifyRunErrorAsync(TestRunNotificationContext context, string errorMessage, bool telegramNotify, CancellationToken ct);
    void ReportExternalResult(bool success, string? error);
}

public sealed record TestRunNotificationContext(
    string RunId,
    string FinalName,
    string ModuleId,
    RunProfile Profile,
    string RelativeRunFolder,
    DateTimeOffset? StartedAtUtc = null);

public sealed class TelegramRunNotifier : ITelegramRunNotifier
{
    private readonly TelegramSettings _settings;
    private readonly ITelegramClient _client;
    private readonly TimeProvider _timeProvider;
    private DateTimeOffset? _lastSentAtUtc;
    private DateTimeOffset? _lastProgressSentAtUtc;
    private string? _lastError;

    public TelegramRunNotifier(TelegramSettings settings, ITelegramClient client, TimeProvider? timeProvider = null)
    {
        _settings = settings;
        _client = client;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public TelegramRuntimeStatus Status => BuildStatus();

    public event EventHandler<TelegramRuntimeStatus>? StatusChanged;

    public Task<TelegramSendResult> NotifyStartAsync(TestRunNotificationContext context, bool telegramNotify, CancellationToken ct)
    {
        if (!telegramNotify || !_settings.NotifyOnStart)
        {
            return Task.FromResult(Skipped());
        }

        var message = $"▶️ Старт: {context.FinalName}\nrun={ShortRunId(context.RunId)} module={context.ModuleId}\n{FormatProfile(context.Profile)}";
        return SendAsync(message, ct);
    }

    public Task<TelegramSendResult> NotifyProgressAsync(TestRunNotificationContext context, ProgressUpdate update, bool telegramNotify, CancellationToken ct)
    {
        if (!telegramNotify || _settings.ProgressMode == ProgressNotifyMode.Off)
        {
            return Task.FromResult(Skipped());
        }

        var now = _timeProvider.GetUtcNow();
        if (_settings.ProgressMode is ProgressNotifyMode.EveryN or ProgressNotifyMode.EveryNRuns)
        {
            var n = Math.Max(1, _settings.ProgressEveryN);
            if (update.Current <= 0 || update.Current % n != 0)
            {
                return Task.FromResult(Skipped());
            }
        }
        else if (_settings.ProgressMode == ProgressNotifyMode.EveryTSeconds)
        {
            var intervalSeconds = Math.Max(1, _settings.ProgressEveryN);
            if (_lastProgressSentAtUtc.HasValue && (now - _lastProgressSentAtUtc.Value).TotalSeconds < intervalSeconds)
            {
                return Task.FromResult(Skipped());
            }
        }

        var elapsed = context.StartedAtUtc.HasValue ? now - context.StartedAtUtc.Value : TimeSpan.Zero;
        var message = $"⏱️ Прогресс: {context.FinalName}\nrun={ShortRunId(context.RunId)} it={update.Current}/{Math.Max(update.Total, 0)} fail? см. отчёт\nelapsed={elapsed:mm\\:ss} p={context.Profile.Parallelism}";
        return SendProgressAsync(message, now, ct);
    }

    public Task<TelegramSendResult> NotifyCompletionAsync(TestReport report, bool telegramNotify, CancellationToken ct)
    {
        if (!telegramNotify)
        {
            return Task.FromResult(Skipped());
        }

        if (report.Status == TestStatus.Failed)
        {
            if (!_settings.NotifyOnError)
            {
                return Task.FromResult(Skipped());
            }

            var errorMessage = $"❌ Ошибка: {report.FinalName}\nrun={ShortRunId(report.RunId)} module={report.ModuleId}\nstatus={report.Status} failed={report.Metrics.FailedItems}";
            return SendAsync(errorMessage, ct);
        }

        if (!_settings.NotifyOnFinish)
        {
            return Task.FromResult(Skipped());
        }

        var prefix = report.Status switch
        {
            TestStatus.Canceled => "🛑 Отмена",
            TestStatus.Stopped => "⏹️ Остановлено",
            _ => "✅ Завершено"
        };

        var message = $"{prefix}: {report.FinalName}\nrun={ShortRunId(report.RunId)} module={report.ModuleId}\nduration={report.Metrics.TotalDurationMs:F0}ms failed={report.Metrics.FailedItems}";
        return SendAsync(message, ct);
    }

    public Task<TelegramSendResult> NotifyRunErrorAsync(TestRunNotificationContext context, string errorMessage, bool telegramNotify, CancellationToken ct)
    {
        if (!telegramNotify || !_settings.NotifyOnError)
        {
            return Task.FromResult(Skipped());
        }

        var text = $"❌ Ошибка запуска: {context.FinalName}\nrun={ShortRunId(context.RunId)} module={context.ModuleId}\n{errorMessage}";
        return SendAsync(text, ct);
    }

    public void ReportExternalResult(bool success, string? error)
    {
        if (success)
        {
            _lastSentAtUtc = _timeProvider.GetUtcNow();
            _lastError = null;
        }
        else
        {
            _lastError = string.IsNullOrWhiteSpace(error) ? "Неизвестная ошибка отправки Telegram." : error;
        }

        RaiseStatusChanged();
    }

    private async Task<TelegramSendResult> SendProgressAsync(string message, DateTimeOffset now, CancellationToken ct)
    {
        var result = await SendAsync(message, ct);
        if (result.Success && string.IsNullOrEmpty(result.Error))
        {
            _lastProgressSentAtUtc = now;
        }

        return result;
    }

    private async Task<TelegramSendResult> SendAsync(string message, CancellationToken ct)
    {
        if (!_settings.Enabled)
        {
            RaiseStatusChanged();
            return Skipped();
        }

        if (!IsConfigured())
        {
            _lastError = "Telegram включён, но BotToken или ChatId не заполнены.";
            RaiseStatusChanged();
            return new TelegramSendResult(false, _lastError);
        }

        var now = _timeProvider.GetUtcNow();
        var rateLimitSeconds = Math.Max(0, _settings.RateLimitSeconds);
        if (_lastSentAtUtc.HasValue && rateLimitSeconds > 0 && (now - _lastSentAtUtc.Value).TotalSeconds < rateLimitSeconds)
        {
            return Skipped();
        }

        var result = await _client.SendMessageAsync(_settings.BotToken, _settings.ChatId, message, ct);
        if (result.Success)
        {
            _lastSentAtUtc = now;
            _lastError = null;
        }
        else
        {
            _lastError = result.Error;
        }

        RaiseStatusChanged();
        return result;
    }

    private TelegramRuntimeStatus BuildStatus()
    {
        if (!_settings.Enabled)
        {
            return new TelegramRuntimeStatus(TelegramIndicatorState.Off, null, _lastSentAtUtc, false);
        }

        if (!IsConfigured())
        {
            return new TelegramRuntimeStatus(TelegramIndicatorState.Error, "BotToken или ChatId не заполнены.", _lastSentAtUtc, false);
        }

        return string.IsNullOrWhiteSpace(_lastError)
            ? new TelegramRuntimeStatus(TelegramIndicatorState.Ok, null, _lastSentAtUtc, true)
            : new TelegramRuntimeStatus(TelegramIndicatorState.Error, _lastError, _lastSentAtUtc, true);
    }

    private bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(_settings.BotToken) && !string.IsNullOrWhiteSpace(_settings.ChatId);
    }

    private void RaiseStatusChanged()
    {
        StatusChanged?.Invoke(this, BuildStatus());
    }

    private static TelegramSendResult Skipped() => new(true, "Skipped");

    private static string FormatProfile(RunProfile profile)
    {
        return $"mode={profile.Mode}; p={profile.Parallelism}; timeout={profile.TimeoutSeconds}s; pause={profile.PauseBetweenIterationsMs}ms";
    }

    private static string ShortRunId(string runId)
    {
        return runId.Length <= 8 ? runId : runId[..8];
    }
}
