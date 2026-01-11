using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Infrastructure.Telegram;

public sealed class TelegramPolicy
{
    private readonly ITelegramNotifier? _notifier;
    private readonly TimeSpan _rateLimit;
    private DateTimeOffset _lastSent = DateTimeOffset.MinValue;

    public TelegramPolicy(ITelegramNotifier? notifier, TimeSpan rateLimit)
    {
        _notifier = notifier;
        _rateLimit = rateLimit;
    }

    public async Task NotifyStartAsync(TestReport report, CancellationToken ct)
    {
        await SendIfAllowedAsync($"Start: {report.Meta.ModuleName} ({report.Meta.ModuleId})", ct);
    }

    public async Task NotifyFinishAsync(TestReport report, CancellationToken ct)
    {
        await SendIfAllowedAsync($"Finish: {report.Meta.ModuleName} Status={report.Meta.Status}", ct);
        if (report.Artifacts.HtmlPath is not null)
        {
            await SendIfAllowedAsync($"Report: {report.Artifacts.HtmlPath}", ct);
        }
    }

    public async Task NotifyErrorAsync(TestReport report, CancellationToken ct)
    {
        await SendIfAllowedAsync($"Error: {report.Meta.ModuleName}", ct);
    }

    private async Task SendIfAllowedAsync(string text, CancellationToken ct)
    {
        if (_notifier == null)
        {
            return;
        }

        if (DateTimeOffset.UtcNow - _lastSent < _rateLimit)
        {
            return;
        }

        _lastSent = DateTimeOffset.UtcNow;
        await _notifier.SendTextAsync(text, ct);
    }
}
