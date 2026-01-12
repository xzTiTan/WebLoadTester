namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

/// <summary>
/// Политика поведения при ошибке шага UI-сценария.
/// </summary>
public enum StepErrorPolicy
{
    SkipStep,
    StopRun,
    StopAll
}

/// <summary>
/// Режимы уведомлений Telegram.
/// </summary>
public enum TelegramNotifyMode
{
    Off,
    OnStart,
    OnFinish,
    OnError,
    OnStartFinish,
    All
}

/// <summary>
/// Режимы прикрепления артефактов к уведомлениям.
/// </summary>
public enum AttachmentsMode
{
    None,
    OnError,
    Final,
    EveryN
}
