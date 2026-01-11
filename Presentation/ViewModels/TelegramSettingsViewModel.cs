using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Telegram;

namespace WebLoadTester.Presentation.ViewModels;

public sealed partial class TelegramSettingsViewModel : ObservableObject
{
    public IReadOnlyList<TelegramProgressMode> ProgressModes { get; } = Enum.GetValues<TelegramProgressMode>();
    public IReadOnlyList<AttachmentsMode> AttachmentModes { get; } = Enum.GetValues<AttachmentsMode>();

    [ObservableProperty]
    private bool enabled;

    [ObservableProperty]
    private string botToken = string.Empty;

    [ObservableProperty]
    private string chatId = string.Empty;

    [ObservableProperty]
    private bool notifyOnStart = true;

    [ObservableProperty]
    private bool notifyOnFinish = true;

    [ObservableProperty]
    private bool notifyOnError = true;

    [ObservableProperty]
    private TelegramProgressMode progressMode = TelegramProgressMode.Off;

    [ObservableProperty]
    private int progressEveryN = 10;

    [ObservableProperty]
    private int progressEverySeconds = 60;

    [ObservableProperty]
    private AttachmentsMode attachmentsMode = AttachmentsMode.Final;

    [ObservableProperty]
    private int attachmentsEveryN = 10;

    [ObservableProperty]
    private int rateLimitSeconds = 10;

    public TelegramPolicySettings ToPolicySettings() => new(
        Enabled,
        NotifyOnStart,
        NotifyOnFinish,
        NotifyOnError,
        ProgressMode,
        ProgressEveryN,
        ProgressEverySeconds,
        AttachmentsMode,
        AttachmentsEveryN,
        RateLimitSeconds);
}
