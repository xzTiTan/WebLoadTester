using System;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Infrastructure.Telegram;

public class TelegramPolicy
{
    private readonly ITelegramNotifier? _notifier;
    private readonly TelegramSettings _settings;
    private DateTimeOffset _lastSent = DateTimeOffset.MinValue;

    public TelegramPolicy(ITelegramNotifier? notifier, TelegramSettings settings)
    {
        _notifier = notifier;
        _settings = settings;
    }

    public bool IsEnabled => _notifier != null && _settings.Enabled;

    public Task NotifyStartAsync(string moduleName, CancellationToken ct)
    {
        if (!IsEnabled || !_settings.NotifyOnStart)
        {
            return Task.CompletedTask;
        }

        return SendAsync($"Started: {moduleName}", ct);
    }

    public Task NotifyProgressAsync(ProgressUpdate update, CancellationToken ct)
    {
        if (!IsEnabled || _settings.ProgressMode == ProgressNotifyMode.Off)
        {
            return Task.CompletedTask;
        }

        if (_settings.ProgressMode == ProgressNotifyMode.EveryNRuns && update.Current % _settings.ProgressEveryN != 0)
        {
            return Task.CompletedTask;
        }

        return SendAsync($"Progress: {update.Current}/{update.Total} {update.Message}", ct);
    }

    public Task NotifyFinishAsync(string moduleName, TestStatus status, CancellationToken ct)
    {
        if (!IsEnabled || !_settings.NotifyOnFinish)
        {
            return Task.CompletedTask;
        }

        return SendAsync($"Finished: {moduleName} ({status})", ct);
    }

    public Task NotifyErrorAsync(string message, CancellationToken ct)
    {
        if (!IsEnabled || !_settings.NotifyOnError)
        {
            return Task.CompletedTask;
        }

        return SendAsync($"Error: {message}", ct);
    }

    private Task SendAsync(string message, CancellationToken ct)
    {
        if (!IsEnabled)
        {
            return Task.CompletedTask;
        }

        if ((DateTimeOffset.Now - _lastSent).TotalSeconds < _settings.RateLimitSeconds)
        {
            return Task.CompletedTask;
        }

        _lastSent = DateTimeOffset.Now;
        return _notifier!.SendTextAsync(message, ct);
    }
}

public class TelegramSettings
{
    public bool Enabled { get; set; }
    public string BotToken { get; set; } = string.Empty;
    public string ChatId { get; set; } = string.Empty;
    public bool NotifyOnStart { get; set; } = true;
    public bool NotifyOnFinish { get; set; } = true;
    public bool NotifyOnError { get; set; } = true;
    public ProgressNotifyMode ProgressMode { get; set; } = ProgressNotifyMode.Off;
    public int ProgressEveryN { get; set; } = 10;
    public int RateLimitSeconds { get; set; } = 10;
    public AttachmentsMode AttachmentsMode { get; set; } = AttachmentsMode.None;
}

public enum ProgressNotifyMode
{
    Off,
    EveryNRuns,
    EveryTSeconds
}
