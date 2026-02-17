using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WebLoadTester.Infrastructure.Telegram;

public sealed class TelegramClient : ITelegramClient
{
    private readonly HttpClient _httpClient;

    public TelegramClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<TelegramSendResult> SendMessageAsync(string botToken, string chatId, string text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(botToken))
        {
            return new TelegramSendResult(false, "BotToken не задан.");
        }

        if (string.IsNullOrWhiteSpace(chatId))
        {
            return new TelegramSendResult(false, "ChatId не задан.");
        }

        var url = $"https://api.telegram.org/bot{botToken}/sendMessage";
        using var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("chat_id", chatId),
            new KeyValuePair<string, string>("text", text)
        });

        try
        {
            using var response = await _httpClient.PostAsync(url, content, ct);
            if (response.IsSuccessStatusCode)
            {
                return new TelegramSendResult(true, string.Empty);
            }

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            var message = string.IsNullOrWhiteSpace(responseBody)
                ? $"Telegram API вернул {(int)response.StatusCode}."
                : $"Telegram API вернул {(int)response.StatusCode}: {responseBody}";
            return new TelegramSendResult(false, message);
        }
        catch (Exception ex)
        {
            return new TelegramSendResult(false, ex.Message);
        }
    }
}
