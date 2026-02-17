using System;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Telegram;
using Xunit;

namespace WebLoadTester.Tests;

public class TelegramProgressTests
{
    [Fact]
    public async Task ProgressModeOff_DoesNotSendProgress()
    {
        var client = new FakeTelegramClient();
        var notifier = CreateNotifier(client, ProgressNotifyMode.Off, progressEveryN: 3, rateLimitSeconds: 0);

        await notifier.NotifyProgressAsync(CreateContext(), new ProgressUpdate(3, 10, "step"), telegramNotify: true, CancellationToken.None);

        Assert.Equal(0, client.SendCalls);
    }

    [Fact]
    public async Task EveryN_SendsOn3_6_9()
    {
        var client = new FakeTelegramClient();
        var notifier = CreateNotifier(client, ProgressNotifyMode.EveryN, progressEveryN: 3, rateLimitSeconds: 0);
        var context = CreateContext();

        for (var i = 1; i <= 9; i++)
        {
            await notifier.NotifyProgressAsync(context, new ProgressUpdate(i, 10, "progress"), telegramNotify: true, CancellationToken.None);
        }

        Assert.Equal(3, client.SendCalls);
    }

    [Fact]
    public async Task EveryN_RespectsRateLimit()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 2, 17, 10, 0, 0, TimeSpan.Zero));
        var client = new FakeTelegramClient();
        var notifier = CreateNotifier(client, ProgressNotifyMode.EveryN, progressEveryN: 3, rateLimitSeconds: 20, timeProvider: time);
        var context = CreateContext(startedAtUtc: time.GetUtcNow());

        await notifier.NotifyProgressAsync(context, new ProgressUpdate(3, 12, "a"), telegramNotify: true, CancellationToken.None);
        await notifier.NotifyProgressAsync(context, new ProgressUpdate(6, 12, "b"), telegramNotify: true, CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(21));
        await notifier.NotifyProgressAsync(context, new ProgressUpdate(9, 12, "c"), telegramNotify: true, CancellationToken.None);

        Assert.Equal(2, client.SendCalls);
    }

    [Fact]
    public async Task EveryTSeconds_SendsNotMoreOftenThanInterval()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 2, 17, 12, 0, 0, TimeSpan.Zero));
        var client = new FakeTelegramClient();
        var notifier = CreateNotifier(client, ProgressNotifyMode.EveryTSeconds, progressEveryN: 10, rateLimitSeconds: 0, timeProvider: time);
        var context = CreateContext(startedAtUtc: time.GetUtcNow());

        await notifier.NotifyProgressAsync(context, new ProgressUpdate(1, 100, "a"), telegramNotify: true, CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(5));
        await notifier.NotifyProgressAsync(context, new ProgressUpdate(2, 100, "b"), telegramNotify: true, CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(5));
        await notifier.NotifyProgressAsync(context, new ProgressUpdate(3, 100, "c"), telegramNotify: true, CancellationToken.None);

        Assert.Equal(2, client.SendCalls);
    }

    [Fact]
    public async Task ProgressSkippedWhenRunTelegramDisabled()
    {
        var client = new FakeTelegramClient();
        var notifier = CreateNotifier(client, ProgressNotifyMode.EveryN, progressEveryN: 1, rateLimitSeconds: 0);

        await notifier.NotifyProgressAsync(CreateContext(), new ProgressUpdate(1, 5, "x"), telegramNotify: false, CancellationToken.None);

        Assert.Equal(0, client.SendCalls);
    }

    [Fact]
    public async Task ProgressError_UpdatesRuntimeStatusWithoutThrowing()
    {
        var client = new FakeTelegramClient { NextFailure = "401 unauthorized" };
        var notifier = CreateNotifier(client, ProgressNotifyMode.EveryN, progressEveryN: 1, rateLimitSeconds: 0);

        var result = await notifier.NotifyProgressAsync(CreateContext(), new ProgressUpdate(1, 2, "x"), telegramNotify: true, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(TelegramIndicatorState.Error, notifier.Status.State);
        Assert.Contains("401", notifier.Status.LastError);
    }

    private static TelegramRunNotifier CreateNotifier(FakeTelegramClient client, ProgressNotifyMode mode, int progressEveryN, int rateLimitSeconds, TimeProvider? timeProvider = null)
    {
        return new TelegramRunNotifier(new TelegramSettings
        {
            Enabled = true,
            BotToken = "token",
            ChatId = "chat",
            ProgressMode = mode,
            ProgressEveryN = progressEveryN,
            RateLimitSeconds = rateLimitSeconds
        }, client, timeProvider);
    }

    private static TestRunNotificationContext CreateContext(DateTimeOffset? startedAtUtc = null)
    {
        return new TestRunNotificationContext(
            "run-123456789",
            "FinalName",
            "http.performance",
            new RunProfile { Parallelism = 2, TimeoutSeconds = 30, PauseBetweenIterationsMs = 100, Mode = RunMode.Iterations },
            "runs/run-123456789",
            startedAtUtc);
    }

    private sealed class FakeTelegramClient : ITelegramClient
    {
        public int SendCalls { get; private set; }
        public string? NextFailure { get; set; }

        public Task<TelegramSendResult> SendMessageAsync(string botToken, string chatId, string text, CancellationToken ct)
        {
            SendCalls++;
            if (!string.IsNullOrWhiteSpace(NextFailure))
            {
                var msg = NextFailure;
                NextFailure = null;
                return Task.FromResult(new TelegramSendResult(false, msg));
            }

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
