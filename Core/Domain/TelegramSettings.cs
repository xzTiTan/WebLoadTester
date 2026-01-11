namespace WebLoadTester.Core.Domain;

public sealed class TelegramSettings
{
    public bool Enabled { get; set; }
    public string BotToken { get; set; } = string.Empty;
    public string ChatId { get; set; } = string.Empty;
    public bool NotifyOnStart { get; set; } = true;
    public bool NotifyOnFinish { get; set; } = true;
    public bool NotifyOnError { get; set; } = true;
    public TelegramNotifyMode ProgressMode { get; set; } = TelegramNotifyMode.Off;
    public AttachmentsMode AttachmentsMode { get; set; } = AttachmentsMode.FinalOnly;
    public int RateLimitSeconds { get; set; } = 10;
}
