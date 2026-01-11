using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.InputFiles;
using WebLoadTester.Core.Contracts;

namespace WebLoadTester.Infrastructure.Telegram;

public class TelegramNotifier : ITelegramNotifier
{
    private readonly ITelegramBotClient _client;
    private readonly ChatId _chatId;

    public TelegramNotifier(string token, string chatId)
    {
        _client = new TelegramBotClient(token);
        _chatId = new ChatId(chatId);
    }

    public Task SendTextAsync(string text, CancellationToken ct)
    {
        return _client.SendTextMessageAsync(_chatId, text, cancellationToken: ct);
    }

    public async Task SendPhotoAsync(string caption, string filePath, CancellationToken ct)
    {
        await using var stream = File.OpenRead(filePath);
        var file = new InputOnlineFile(stream, Path.GetFileName(filePath));
        await _client.SendPhotoAsync(_chatId, file, caption: caption, cancellationToken: ct);
    }

    public async Task SendDocumentAsync(string caption, string filePath, CancellationToken ct)
    {
        await using var stream = File.OpenRead(filePath);
        var file = new InputOnlineFile(stream, Path.GetFileName(filePath));
        await _client.SendDocumentAsync(_chatId, file, caption: caption, cancellationToken: ct);
    }
}
