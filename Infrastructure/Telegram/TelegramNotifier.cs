using System.IO;
using Telegram.Bot;
using Telegram.Bot.Types;
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
        return TelegramBotClientExtensions.SendTextMessageAsync(_client, _chatId, text, cancellationToken: ct);
    }

    public async Task SendPhotoAsync(string caption, string filePath, CancellationToken ct)
    {
        await using var stream = File.OpenRead(filePath);
        var file = InputFile.FromStream(stream, Path.GetFileName(filePath));
        await TelegramBotClientExtensions.SendPhotoAsync(_client, _chatId, file, caption: caption, cancellationToken: ct);
    }

    public async Task SendDocumentAsync(string caption, string filePath, CancellationToken ct)
    {
        await using var stream = File.OpenRead(filePath);
        var file = InputFile.FromStream(stream, Path.GetFileName(filePath));
        await TelegramBotClientExtensions.SendDocumentAsync(_client, _chatId, file, caption: caption, cancellationToken: ct);
    }
}
