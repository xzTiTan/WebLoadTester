using System;
using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Presentation.ViewModels;

public partial class TelegramSettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _enabled;

    [ObservableProperty]
    private string _botToken = string.Empty;

    [ObservableProperty]
    private string _chatId = string.Empty;

    [ObservableProperty]
    private bool _notifyOnStart = true;

    [ObservableProperty]
    private bool _notifyOnFinish = true;

    [ObservableProperty]
    private bool _notifyOnError = true;

    [ObservableProperty]
    private TelegramNotifyMode _progressMode = TelegramNotifyMode.Off;

    [ObservableProperty]
    private AttachmentsMode _attachmentsMode = AttachmentsMode.FinalOnly;

    [ObservableProperty]
    private int _rateLimitSeconds = 10;

    public TelegramNotifyMode[] ProgressModeValues { get; } = Enum.GetValues<TelegramNotifyMode>();

    public AttachmentsMode[] AttachmentsModeValues { get; } = Enum.GetValues<AttachmentsMode>();

    public TelegramSettings ToSettings() => new()
    {
        Enabled = Enabled,
        BotToken = BotToken,
        ChatId = ChatId,
        NotifyOnStart = NotifyOnStart,
        NotifyOnFinish = NotifyOnFinish,
        NotifyOnError = NotifyOnError,
        ProgressMode = ProgressMode,
        AttachmentsMode = AttachmentsMode,
        RateLimitSeconds = RateLimitSeconds
    };
}
