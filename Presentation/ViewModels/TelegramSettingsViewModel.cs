using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Presentation.ViewModels;

public partial class TelegramSettingsViewModel : ObservableObject
{
    public IReadOnlyList<TelegramNotifyMode> NotifyModes { get; } = Enum.GetValues<TelegramNotifyMode>();
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
    private TelegramNotifyMode notifyMode = TelegramNotifyMode.OnStartFinish;

    [ObservableProperty]
    private int rateLimitSeconds = 10;
}
