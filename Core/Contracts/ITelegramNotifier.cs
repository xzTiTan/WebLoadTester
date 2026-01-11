using System.Threading;
using System.Threading.Tasks;

namespace WebLoadTester.Core.Contracts;

public interface ITelegramNotifier
{
    Task SendTextAsync(string text, CancellationToken ct);
    Task SendPhotoAsync(string caption, string filePath, CancellationToken ct);
    Task SendDocumentAsync(string caption, string filePath, CancellationToken ct);
}
