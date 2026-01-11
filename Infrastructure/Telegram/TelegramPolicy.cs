using System;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Infrastructure.Telegram;

/// <summary>
/// Политика отправки уведомлений в Telegram с учётом настроек.
/// </summary>
public class TelegramPolicy
{
    private readonly ITelegramNotifier? _notifier;
    private readonly TelegramSettings _settings;
    private DateTimeOffset _lastSent = DateTimeOffset.MinValue;

    /// <summary>
    /// Инициализирует политику с уведомителем и настройками.
    /// </summary>
    public TelegramPolicy(ITelegramNotifier? notifier, TelegramSettings settings)
    {
        _notifier = notifier;
        _settings = settings;
    }

    /// <summary>
    /// Показывает, можно ли отправлять уведомления.
    /// </summary>
    public bool IsEnabled => _notifier != null && _settings.Enabled;

    /// <summary>
    /// Отправляет уведомление о старте, если это разрешено настройками.
    /// </summary>
    public Task NotifyStartAsync(string moduleName, CancellationToken ct)
    {
        if (!IsEnabled || !_settings.NotifyOnStart)
        {
            return Task.CompletedTask;
        }

        return SendAsync($"Started: {moduleName}", ct);
    }

    /// <summary>
    /// Отправляет уведомление о прогрессе, если включено.
    /// </summary>
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

    /// <summary>
    /// Отправляет уведомление о завершении.
    /// </summary>
    public Task NotifyFinishAsync(string moduleName, TestStatus status, CancellationToken ct)
    {
        if (!IsEnabled || !_settings.NotifyOnFinish)
        {
            return Task.CompletedTask;
        }

        return SendAsync($"Finished: {moduleName} ({status})", ct);
    }

    /// <summary>
    /// Отправляет уведомление об ошибке.
    /// </summary>
    public Task NotifyErrorAsync(string message, CancellationToken ct)
    {
        if (!IsEnabled || !_settings.NotifyOnError)
        {
            return Task.CompletedTask;
        }

        return SendAsync($"Error: {message}", ct);
    }

    /// <summary>
    /// Внутренняя отправка сообщения с учётом лимита частоты.
    /// </summary>
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

/// <summary>
/// Настройки Telegram-уведомлений.
/// </summary>
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

/// <summary>
/// Режимы отправки прогресса в Telegram.
/// </summary>
public enum ProgressNotifyMode
{
    Off,
    EveryNRuns,
    EveryTSeconds
}
