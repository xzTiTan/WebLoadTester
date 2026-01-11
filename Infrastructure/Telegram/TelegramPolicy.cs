using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Infrastructure.Telegram;

public sealed class TelegramPolicy
{
    private readonly ITelegramNotifier? _notifier;
    private readonly TelegramPolicySettings _settings;
    private DateTimeOffset _lastMessageAt = DateTimeOffset.MinValue;

    public TelegramPolicy(ITelegramNotifier? notifier, TelegramPolicySettings settings)
    {
        _notifier = notifier;
        _settings = settings;
    }

    public async Task NotifyStartAsync(string moduleName, CancellationToken ct)
    {
        if (!_settings.Enabled || _notifier is null || !_settings.NotifyOnStart)
        {
            return;
        }

        await SendAsync($"Started: {moduleName}", ct);
    }

    public async Task NotifyErrorAsync(string moduleName, string error, CancellationToken ct)
    {
        if (!_settings.Enabled || _notifier is null || !_settings.NotifyOnError)
        {
            return;
        }

        await SendAsync($"Error in {moduleName}: {error}", ct);
    }

    public async Task NotifyFinishAsync(TestReport report, CancellationToken ct)
    {
        if (!_settings.Enabled || _notifier is null || !_settings.NotifyOnFinish)
        {
            return;
        }

        await SendAsync($"Finished: {report.ModuleName} ({report.Status})", ct);

        if (_settings.AttachmentsMode == AttachmentsMode.Final && report.Artifacts.HtmlPath is not null)
        {
            await _notifier.SendDocumentAsync("Report", report.Artifacts.HtmlPath, ct);
        }
    }

    private async Task SendAsync(string text, CancellationToken ct)
    {
        var now = DateTimeOffset.Now;
        if ((now - _lastMessageAt).TotalSeconds < _settings.RateLimitSeconds)
        {
            return;
        }

        _lastMessageAt = now;
        await _notifier.SendTextAsync(text, ct);
    }
}

public sealed record TelegramPolicySettings(
    bool Enabled,
    bool NotifyOnStart,
    bool NotifyOnFinish,
    bool NotifyOnError,
    TelegramProgressMode ProgressMode,
    int ProgressEveryN,
    int ProgressEverySeconds,
    AttachmentsMode AttachmentsMode,
    int AttachmentsEveryN,
    int RateLimitSeconds);
