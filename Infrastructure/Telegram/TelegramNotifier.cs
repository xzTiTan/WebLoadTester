using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types.InputFiles;
using WebLoadTester.Core.Contracts;

namespace WebLoadTester.Infrastructure.Telegram;

public sealed class TelegramNotifier : ITelegramNotifier
{
    private readonly ITelegramBotClient _client;
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

    public async Task SendPhotoAsync(string caption, string filePath, CancellationToken ct)
    {
        await using var stream = File.OpenRead(filePath);
        var input = InputFile.FromStream(stream, Path.GetFileName(filePath));
        await _client.SendPhotoAsync(_chatId, input, caption: caption, cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task SendDocumentAsync(string caption, string filePath, CancellationToken ct)
    {
        await using var stream = File.OpenRead(filePath);
        var input = InputFile.FromStream(stream, Path.GetFileName(filePath));
        await _client.SendDocumentAsync(_chatId, input, caption: caption, cancellationToken: ct).ConfigureAwait(false);
    }
}
