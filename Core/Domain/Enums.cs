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
    OnError
}

public enum TelegramProgressMode
{
    Off,
    EveryNRuns,
    EveryTSeconds
}

public enum AttachmentsMode
{
    None,
    OnError,
    Final,
    EveryN
}
