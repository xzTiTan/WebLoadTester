using Telegram.Bot;
using Telegram.Bot.Types.InputFiles;
using WebLoadTester.Core.Contracts;

namespace WebLoadTester.Infrastructure.Telegram;

public sealed class TelegramNotifier : ITelegramNotifier
{
    private readonly TelegramBotClient _client;
    private readonly string _chatId;

    public TelegramNotifier(string token, string chatId)
    {
        _client = new TelegramBotClient(token);
        _chatId = chatId;
    }

    public async Task SendTextAsync(string text, CancellationToken ct)
    {
        await _client.SendTextMessageAsync(_chatId, text, cancellationToken: ct);
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
