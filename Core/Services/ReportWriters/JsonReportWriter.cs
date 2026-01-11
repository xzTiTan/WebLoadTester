using System.Text.Json;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Services.ReportWriters;

public sealed class JsonReportWriter
{
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public string Write(TestReport report, string path)
    {
        var json = JsonSerializer.Serialize(report, _options);
        File.WriteAllText(path, json);
        return path;
    }
}
