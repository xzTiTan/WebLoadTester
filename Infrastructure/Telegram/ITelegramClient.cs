using System.Threading;
using System.Threading.Tasks;

namespace WebLoadTester.Infrastructure.Telegram;

public interface ITelegramClient
{
    Task<TelegramSendResult> SendMessageAsync(string botToken, string chatId, string text, CancellationToken ct);
}

public sealed record TelegramSendResult(bool Success, string Error);
