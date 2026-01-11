using System.Collections.Generic;
using System.Linq;
using WebLoadTester.Core.Contracts;

namespace WebLoadTester.Core.Services;

public class ModuleRegistry
{
    private readonly List<ITestModule> _modules = new();

    public ModuleRegistry(IEnumerable<ITestModule> modules)
    {
        _modules.AddRange(modules);
    }

    public IReadOnlyList<ITestModule> Modules => _modules;

    public IEnumerable<ITestModule> GetByFamily(Core.Domain.TestFamily family)
        => _modules.Where(m => m.Family == family);
}
