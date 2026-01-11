using System;
using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Infrastructure.Telegram;

namespace WebLoadTester.Presentation.ViewModels;

public partial class TelegramSettingsViewModel : ObservableObject
{
    private readonly TelegramSettings _settings;

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

    public TelegramSettings Settings => _settings;
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

    partial void OnEnabledChanged(bool value) => _settings.Enabled = value;
    partial void OnBotTokenChanged(string value) => _settings.BotToken = value;
    partial void OnChatIdChanged(string value) => _settings.ChatId = value;
    partial void OnNotifyOnStartChanged(bool value) => _settings.NotifyOnStart = value;
    partial void OnNotifyOnFinishChanged(bool value) => _settings.NotifyOnFinish = value;
    partial void OnNotifyOnErrorChanged(bool value) => _settings.NotifyOnError = value;
    partial void OnProgressModeChanged(ProgressNotifyMode value) => _settings.ProgressMode = value;
    partial void OnProgressEveryNChanged(int value) => _settings.ProgressEveryN = value;
    partial void OnRateLimitSecondsChanged(int value) => _settings.RateLimitSeconds = value;
}
