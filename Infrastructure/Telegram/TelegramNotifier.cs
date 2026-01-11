using System.IO;
using System.Net.Http;
using WebLoadTester.Core.Contracts;

namespace WebLoadTester.Infrastructure.Telegram;

/// <summary>
/// Реализация уведомителя Telegram через HTTP API.
/// </summary>
public class TelegramNotifier : ITelegramNotifier
{
    private readonly string _token;
    private readonly string _chatId;
    private readonly HttpClient _httpClient = new();

    /// <summary>
    /// Создаёт уведомитель с заданным токеном и chatId.
    /// </summary>
    public TelegramNotifier(string token, string chatId)
    {
        _token = token;
        _chatId = chatId;
    }

    /// <summary>
    /// Отправляет текстовое сообщение в Telegram.
    /// </summary>
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

    /// <summary>
    /// Отправляет фото с подписью.
    /// </summary>
    public Task SendPhotoAsync(string caption, string filePath, CancellationToken ct)
    {
        return SendFileAsync("sendPhoto", "photo", caption, filePath, ct);
    }

    /// <summary>
    /// Отправляет документ с подписью.
    /// </summary>
    public Task SendDocumentAsync(string caption, string filePath, CancellationToken ct)
    {
        return SendFileAsync("sendDocument", "document", caption, filePath, ct);
    }

    /// <summary>
    /// Отправляет файл через Telegram API.
    /// </summary>
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

    /// <summary>
    /// Формирует URL запроса к Telegram API.
    /// </summary>
    private string BuildUrl(string method) => $"https://api.telegram.org/bot{_token}/{method}";
}
