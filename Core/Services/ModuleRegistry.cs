using System.Collections.Generic;
using System.Linq;
using WebLoadTester.Core.Contracts;

namespace WebLoadTester.Core.Services;

/// <summary>
/// Реестр модулей с возможностью фильтрации по семейству.
/// </summary>
public class ModuleRegistry
{
    private readonly List<ITestModule> _modules = new();

    /// <summary>
    /// Инициализирует реестр заданными модулями.
    /// </summary>
    public ModuleRegistry(IEnumerable<ITestModule> modules)
    {
        _modules.AddRange(modules);
    }

    /// <summary>
    /// Полный список зарегистрированных модулей.
    /// </summary>
    public IReadOnlyList<ITestModule> Modules => _modules;

    /// <summary>
    /// Возвращает модули заданного семейства.
    /// </summary>
    public IEnumerable<ITestModule> GetByFamily(Core.Domain.TestFamily family)
        => _modules.Where(m => m.Family == family);
}
