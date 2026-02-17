using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Telegram;
using Xunit;

namespace WebLoadTester.Tests;

public class TelegramRunNotifierTests
{
    [Fact]
    public async Task NotifyStart_SkipsWhenProfileTelegramDisabled()
    {
        var client = new FakeTelegramClient();
        var notifier = new TelegramRunNotifier(new TelegramSettings
        {
            Enabled = true,
            BotToken = "token",
            ChatId = "chat",
            NotifyOnStart = true
        }, client, new FakeTimeProvider(DateTimeOffset.UtcNow));

        var result = await notifier.NotifyStartAsync(CreateContext(), telegramNotify: false, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0, client.SendCalls);
    }

    [Fact]
    public async Task NotifyCompletion_RoutesFinishAndErrorByStatus()
    {
        var client = new FakeTelegramClient();
        var notifier = new TelegramRunNotifier(new TelegramSettings
        {
            Enabled = true,
            BotToken = "token",
            ChatId = "chat",
            NotifyOnFinish = true,
            NotifyOnError = true,
            RateLimitSeconds = 0
        }, client, new FakeTimeProvider(DateTimeOffset.UtcNow));

        await notifier.NotifyCompletionAsync(CreateReport(TestStatus.Success), telegramNotify: true, CancellationToken.None);
        await notifier.NotifyCompletionAsync(CreateReport(TestStatus.Failed), telegramNotify: true, CancellationToken.None);

        Assert.Equal(2, client.SendCalls);
        Assert.Contains("✅ Завершено", client.Messages[0]);
        Assert.Contains("❌ Ошибка", client.Messages[1]);
    }

    [Fact]
    public async Task NotifyCompletion_RespectsRateLimitDeterministically()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 2, 17, 12, 0, 0, TimeSpan.Zero));
        var client = new FakeTelegramClient();
        var notifier = new TelegramRunNotifier(new TelegramSettings
        {
            Enabled = true,
            BotToken = "token",
            ChatId = "chat",
            NotifyOnStart = true,
            RateLimitSeconds = 10
        }, client, fakeTime);

        await notifier.NotifyStartAsync(CreateContext(), telegramNotify: true, CancellationToken.None);
        await notifier.NotifyStartAsync(CreateContext() with { RunId = "run-2" }, telegramNotify: true, CancellationToken.None);
        fakeTime.Advance(TimeSpan.FromSeconds(11));
        await notifier.NotifyStartAsync(CreateContext() with { RunId = "run-3" }, telegramNotify: true, CancellationToken.None);

        Assert.Equal(2, client.SendCalls);
    }

    private static TestRunNotificationContext CreateContext()
    {
        return new TestRunNotificationContext(
            "run-1",
            "Smoke",
            "http.functional",
            new RunProfile { Mode = RunMode.Iterations, Parallelism = 2, TimeoutSeconds = 30, PauseBetweenIterationsMs = 100 },
            "runs/run-1");
    }

    private static TestReport CreateReport(TestStatus status)
    {
        return new TestReport
        {
            RunId = status == TestStatus.Success ? "run-s" : "run-f",
            FinalName = "Case",
            ModuleId = "http.functional",
            Status = status,
            ProfileSnapshot = new RunProfile { Mode = RunMode.Iterations, Parallelism = 1, TimeoutSeconds = 15, PauseBetweenIterationsMs = 50 },
            Metrics = new MetricsSummary { FailedItems = status == TestStatus.Failed ? 1 : 0, TotalDurationMs = 1234 }
        };
    }

    private sealed class FakeTelegramClient : ITelegramClient
    {
        public int SendCalls { get; private set; }
        public System.Collections.Generic.List<string> Messages { get; } = new();

        public Task<TelegramSendResult> SendMessageAsync(string botToken, string chatId, string text, CancellationToken ct)
        {
            SendCalls++;
            Messages.Add(text);
            return Task.FromResult(new TelegramSendResult(true, string.Empty));
        }
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta)
        {
            _utcNow = _utcNow.Add(delta);
        }
    }
}
