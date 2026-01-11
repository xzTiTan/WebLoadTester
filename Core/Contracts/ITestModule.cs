using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Contracts;

public interface ITestModule
{
    string Id { get; }
    string DisplayName { get; }
    TestFamily Family { get; }
    Type SettingsType { get; }
    object CreateDefaultSettings();
    IReadOnlyList<string> Validate(object settings);
    Task<TestReport> RunAsync(object settings, IRunContext ctx, CancellationToken ct);
}
