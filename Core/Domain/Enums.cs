namespace WebLoadTester.Core.Domain;

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
/// Режим ожидания загрузки страницы для Playwright-навигации.
/// </summary>
public enum UiWaitUntil
{
    DomContentLoaded,
    Load,
    NetworkIdle
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
