using System;
using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Infrastructure.Telegram;

namespace WebLoadTester.Presentation.ViewModels;

/// <summary>
/// ViewModel для настроек Telegram-уведомлений.
/// </summary>
public partial class TelegramSettingsViewModel : ObservableObject
{
    private readonly TelegramSettings _settings;

    /// <summary>
    /// Инициализирует ViewModel и копирует значения из настроек.
    /// </summary>
    public TelegramSettingsViewModel(TelegramSettings settings)
    {
        _settings = settings;
        enabled = settings.Enabled;
        botToken = settings.BotToken;
        chatId = settings.ChatId;
        notifyOnStart = settings.NotifyOnStart;
        notifyOnFinish = settings.NotifyOnFinish;
        notifyOnError = settings.NotifyOnError;
        progressMode = settings.ProgressMode;
        progressEveryN = settings.ProgressEveryN;
        rateLimitSeconds = settings.RateLimitSeconds;
    }

    /// <summary>
    /// Возвращает исходный объект настроек для использования при запуске.
    /// </summary>
    public TelegramSettings Settings => _settings;
    /// <summary>
    /// Доступные режимы уведомления прогресса для списка в UI.
    /// </summary>
    public Array ProgressModes { get; } = Enum.GetValues(typeof(ProgressNotifyMode));

    [ObservableProperty]
    private bool enabled;

    [ObservableProperty]
    private string botToken = string.Empty;

    [ObservableProperty]
    private string chatId = string.Empty;

    [ObservableProperty]
    private bool notifyOnStart;

    [ObservableProperty]
    private bool notifyOnFinish;

    [ObservableProperty]
    private bool notifyOnError;

    [ObservableProperty]
    private ProgressNotifyMode progressMode;

    [ObservableProperty]
    private int progressEveryN;

    [ObservableProperty]
    private int rateLimitSeconds;

    /// <summary>
    /// Синхронизирует флаг включения с моделью настроек.
    /// </summary>
    partial void OnEnabledChanged(bool value) => _settings.Enabled = value;
    /// <summary>
    /// Синхронизирует токен бота с моделью настроек.
    /// </summary>
    partial void OnBotTokenChanged(string value) => _settings.BotToken = value;
    /// <summary>
    /// Синхронизирует chat id с моделью настроек.
    /// </summary>
    partial void OnChatIdChanged(string value) => _settings.ChatId = value;
    /// <summary>
    /// Синхронизирует настройку уведомления о старте.
    /// </summary>
    partial void OnNotifyOnStartChanged(bool value) => _settings.NotifyOnStart = value;
    /// <summary>
    /// Синхронизирует настройку уведомления о завершении.
    /// </summary>
    partial void OnNotifyOnFinishChanged(bool value) => _settings.NotifyOnFinish = value;
    /// <summary>
    /// Синхронизирует настройку уведомления об ошибке.
    /// </summary>
    partial void OnNotifyOnErrorChanged(bool value) => _settings.NotifyOnError = value;
    /// <summary>
    /// Синхронизирует режим уведомлений о прогрессе.
    /// </summary>
    partial void OnProgressModeChanged(ProgressNotifyMode value) => _settings.ProgressMode = value;
    /// <summary>
    /// Синхронизирует периодичность уведомлений по количеству запусков.
    /// </summary>
    partial void OnProgressEveryNChanged(int value) => _settings.ProgressEveryN = value;
    /// <summary>
    /// Синхронизирует ограничение частоты отправки сообщений.
    /// </summary>
    partial void OnRateLimitSecondsChanged(int value) => _settings.RateLimitSeconds = value;
}
