using System.Threading;
using System.Threading.Tasks;

namespace WebLoadTester.Core.Contracts;

/// <summary>
/// Контракт для отправки уведомлений и файлов в Telegram.
/// </summary>
public interface ITelegramNotifier
{
    /// <summary>
    /// Отправляет текстовое сообщение.
    /// </summary>
    Task SendTextAsync(string text, CancellationToken ct);
    /// <summary>
    /// Отправляет фотографию с подписью.
    /// </summary>
    Task SendPhotoAsync(string caption, string filePath, CancellationToken ct);
    /// <summary>
    /// Отправляет документ с подписью.
    /// </summary>
    Task SendDocumentAsync(string caption, string filePath, CancellationToken ct);
}
