using WebLoadTester.Core.Contracts;

namespace WebLoadTester.Core.Services;

public sealed class ModuleRegistry
{
    private readonly List<ITestModule> _modules = [];

    public IReadOnlyList<ITestModule> Modules => _modules;

    public void Register(ITestModule module) => _modules.Add(module);

    public IEnumerable<ITestModule> GetByFamily(Core.Domain.TestFamily family)
        => _modules.Where(m => m.Family == family);
}
