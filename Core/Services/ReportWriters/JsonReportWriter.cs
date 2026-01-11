using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Services.ReportWriters;

public static class JsonReportWriter
{
    public static async Task<string> WriteAsync(TestReport report, IArtifactStore store)
    {
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        var path = await store.SaveJsonAsync(report).ConfigureAwait(false);
        await File.WriteAllTextAsync(path, json).ConfigureAwait(false);
        return path;
    }
}
