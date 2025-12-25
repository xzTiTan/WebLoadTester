using WebLoadTester.Services;

namespace WebLoadTester.Domain
{
    public class RunRequest
    {
        public RunSettings Settings { get; set; } = new();
        public Scenario Scenario { get; set; } = new();
        public int WorkerId { get; set; }
        public int RunId { get; set; }
        public string PhaseName { get; set; } = string.Empty;
        public ILogSink Logger { get; set; } = default!;
        public System.Threading.CancellationTokenSource? CancelAll { get; set; }
    }
}
