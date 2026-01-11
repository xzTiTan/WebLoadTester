using System;
using System.Collections.Generic;
using System.Linq;
using WebLoadTester.Core.Contracts;

namespace WebLoadTester.Core.Services;

public sealed class ModuleRegistry
{
    private readonly List<ITestModule> _modules = new();

    public IReadOnlyList<ITestModule> Modules => _modules;

    public void Register(ITestModule module)
    {
        if (_modules.Any(m => m.Id == module.Id))
        {
            throw new InvalidOperationException($"Module with id '{module.Id}' already registered.");
        }

        _modules.Add(module);
    }

    public IEnumerable<ITestModule> GetByFamily(Core.Domain.TestFamily family) =>
        _modules.Where(m => m.Family == family);
}
