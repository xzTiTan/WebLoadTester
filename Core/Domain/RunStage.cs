namespace WebLoadTester.Core.Domain;

/// <summary>
/// Стадии жизненного цикла прогона.
/// </summary>
public enum RunStage
{
    Idle,
    Preflight,
    Running,
    Saving,
    Done
}

/// <summary>
/// Контейнер события смены стадии прогона.
/// </summary>
public class RunStageChangedEventArgs : System.EventArgs
{
    public RunStageChangedEventArgs(RunStage stage, string? message = null)
    {
        Stage = stage;
        Message = message;
    }

    public RunStage Stage { get; }
    public string? Message { get; }
}
