namespace WebLoadTester.Core.Domain;

public enum StepErrorPolicy
{
    SkipStep,
    StopRun,
    StopAll
}

public enum TelegramNotifyMode
{
    Off,
    OnStart,
    OnFinish,
    OnError,
    OnStartFinish,
    All
}

public enum AttachmentsMode
{
    None,
    OnError,
    Final,
    EveryN
}
