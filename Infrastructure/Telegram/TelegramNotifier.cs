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

    public Task SendTextAsync(string text, CancellationToken ct)
    {
        return _client.SendTextMessageAsync(_chatId, text, cancellationToken: ct);
    }

    public Task SendPhotoAsync(string caption, string filePath, CancellationToken ct)
    {
        return _client.SendPhotoAsync(_chatId, InputFile.FromStream(File.OpenRead(filePath)), caption: caption, cancellationToken: ct);
    }

    public Task SendDocumentAsync(string caption, string filePath, CancellationToken ct)
    {
        return _client.SendDocumentAsync(_chatId, InputFile.FromStream(File.OpenRead(filePath)), caption: caption, cancellationToken: ct);
    }
}
