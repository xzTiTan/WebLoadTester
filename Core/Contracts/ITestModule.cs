using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Contracts;

/// <summary>
/// Контракт тестового модуля: валидация, настройки и запуск.
/// </summary>
public interface ITestModule
{
    /// <summary>
    /// Уникальный идентификатор модуля.
    /// </summary>
    string Id { get; }
    /// <summary>
    /// Отображаемое имя модуля.
    /// </summary>
    string DisplayName { get; }
    /// <summary>
    /// Семейство тестов для группировки в UI.
    /// </summary>
    TestFamily Family { get; }
    /// <summary>
    /// Тип объекта настроек для данного модуля.
    /// </summary>
    Type SettingsType { get; }
    /// <summary>
    /// Создаёт настройки по умолчанию.
    /// </summary>
    object CreateDefaultSettings();
    /// <summary>
    /// Проверяет корректность настроек и возвращает список ошибок.
    /// </summary>
    IReadOnlyList<string> Validate(object settings);
    /// <summary>
    /// Запускает тест асинхронно и возвращает отчёт.
    /// </summary>
    Task<TestReport> RunAsync(object settings, IRunContext ctx, CancellationToken ct);
}
