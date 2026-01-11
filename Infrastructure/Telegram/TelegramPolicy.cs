using System;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Infrastructure.Telegram;

public sealed class TelegramPolicy
{
    private readonly ITelegramNotifier? _notifier;
    private readonly TelegramSettings _settings;
    private DateTimeOffset _lastSent = DateTimeOffset.MinValue;

    public TelegramPolicy(ITelegramNotifier? notifier, TelegramSettings settings)
    {
        _notifier = notifier;
        _settings = settings;
    }

    public bool IsEnabled => _settings.Enabled && _notifier is not null &&
                             !string.IsNullOrWhiteSpace(_settings.BotToken) &&
                             !string.IsNullOrWhiteSpace(_settings.ChatId);

    public Task NotifyStartAsync(string message, CancellationToken ct) =>
        TrySendAsync(_settings.NotifyOnStart, message, ct);

    public Task NotifyProgressAsync(string message, CancellationToken ct) =>
        TrySendAsync(_settings.ProgressMode == TelegramNotifyMode.EveryProgress, message, ct);

    public Task NotifyErrorAsync(string message, CancellationToken ct) =>
        TrySendAsync(_settings.NotifyOnError, message, ct);

    public Task NotifyFinishAsync(string message, CancellationToken ct) =>
        TrySendAsync(_settings.NotifyOnFinish, message, ct);

    private async Task TrySendAsync(bool shouldSend, string message, CancellationToken ct)
    {
        if (!IsEnabled || !shouldSend)
        {
            return;
        }

        if ((DateTimeOffset.Now - _lastSent).TotalSeconds < _settings.RateLimitSeconds)
        {
            return;
        }

        _lastSent = DateTimeOffset.Now;
        await _notifier!.SendTextAsync(message, ct).ConfigureAwait(false);
    }
}
