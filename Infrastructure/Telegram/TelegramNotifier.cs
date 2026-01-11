using System.IO;
using System.Net.Http;
using WebLoadTester.Core.Contracts;

namespace WebLoadTester.Infrastructure.Telegram;

public class TelegramNotifier : ITelegramNotifier
{
    private readonly string _token;
    private readonly string _chatId;
    private readonly HttpClient _httpClient = new();

    public TelegramNotifier(string token, string chatId)
    {
        _token = token;
        _chatId = chatId;
    }

    public async Task SendTextAsync(string text, CancellationToken ct)
    {
        var url = BuildUrl("sendMessage");
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("chat_id", _chatId),
            new KeyValuePair<string, string>("text", text)
        });
        using var response = await _httpClient.PostAsync(url, content, ct);
        response.EnsureSuccessStatusCode();
    }

    public Task SendPhotoAsync(string caption, string filePath, CancellationToken ct)
    {
        return SendFileAsync("sendPhoto", "photo", caption, filePath, ct);
    }

    public Task SendDocumentAsync(string caption, string filePath, CancellationToken ct)
    {
        return SendFileAsync("sendDocument", "document", caption, filePath, ct);
    }

    private async Task SendFileAsync(string endpoint, string fileField, string caption, string filePath, CancellationToken ct)
    {
        var url = BuildUrl(endpoint);
        await using var stream = File.OpenRead(filePath);
        using var content = new MultipartFormDataContent
        {
            { new StringContent(_chatId), "chat_id" },
            { new StringContent(caption), "caption" },
            { new StreamContent(stream), fileField, Path.GetFileName(filePath) }
        };
        using var response = await _httpClient.PostAsync(url, content, ct);
        response.EnsureSuccessStatusCode();
    }

    private string BuildUrl(string method) => $"https://api.telegram.org/bot{_token}/{method}";
}
