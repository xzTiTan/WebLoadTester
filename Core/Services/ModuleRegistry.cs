using WebLoadTester.Core.Contracts;

namespace WebLoadTester.Core.Services;

public sealed class ModuleRegistry
{
    private readonly List<ITestModule> _modules = new();

    public ModuleRegistry(IEnumerable<ITestModule> modules)
    {
        _modules.AddRange(modules);
    }

    public IReadOnlyList<ITestModule> Modules => _modules;

    public IReadOnlyList<ITestModule> ByFamily(TestFamily family) => _modules.Where(m => m.Family == family).ToList();

    public ITestModule? Find(string id) => _modules.FirstOrDefault(m => m.Id == id);
}
