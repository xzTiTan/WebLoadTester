using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Domain.Reporting;

namespace WebLoadTester.Reports
{
    public class JsonReportWriter
    {
        public async Task<string> WriteAsync(TestReport report, string timestamp, CancellationToken ct)
        {
            var stamp = string.IsNullOrWhiteSpace(timestamp) ? DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") : timestamp;
            Directory.CreateDirectory("reports");
            var file = Path.Combine("reports", $"report_{stamp}.json");

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            await using var stream = File.Create(file);
            await JsonSerializer.SerializeAsync(stream, report, options, ct);
            return file;
        }
    }
}
