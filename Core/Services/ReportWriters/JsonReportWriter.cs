using System.Text.Json;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Services.ReportWriters;

public sealed class JsonReportWriter
{
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true
    };

    public string Serialize(TestReport report) => JsonSerializer.Serialize(report, _options);
}
