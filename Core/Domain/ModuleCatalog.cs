using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WebLoadTester.Core.Domain;

/// <summary>
/// Единый каталог модулей и их UI-метаданных для MVP.
/// </summary>
public static class ModuleCatalog
{
    private static readonly IReadOnlyDictionary<string, ModuleDescriptor> DescriptorsById =
        new ReadOnlyDictionary<string, ModuleDescriptor>(new Dictionary<string, ModuleDescriptor>(StringComparer.OrdinalIgnoreCase)
        {
            ["ui.scenario"] = new("ui.scenario", "3. Регрессионное тестирование", "Повторный запуск пользовательского сценария с фиксацией результатов и сравнением шагов.", TestFamily.UiTesting, "UIСценарий"),
            ["ui.snapshot"] = new("ui.snapshot", "4. Интерфейсное тестирование", "Проверка визуального состояния страниц и элементов через скриншоты.", TestFamily.UiTesting, "UISнимки"),
            ["ui.timing"] = new("ui.timing", "5. Тестирование совместимости", "Проверка прохождения теста на наборах профилей браузера и viewport.", TestFamily.UiTesting, "UITайминги"),
            ["http.functional"] = new("http.functional", "2. Функциональное тестирование", "Функциональные HTTP-проверки с ассерциями ожидаемого поведения.", TestFamily.HttpTesting, "HTTPФункциональные"),
            ["http.performance"] = new("http.performance", "6. Тестирование производительности", "Измерение отклика Web-сайта под управляемой нагрузкой.", TestFamily.HttpTesting, "HTTPПроизводительность"),
            ["http.assets"] = new("http.assets", "10. Тестирование ресурсов Web-сайта", "Проверка доступности и характеристик статических ресурсов сайта.", TestFamily.HttpTesting, "HTTPАссеты"),
            ["net.diagnostics"] = new("net.diagnostics", "9. Диагностическое тестирование", "Диагностика DNS, TCP и TLS для локализации сетевых проблем.", TestFamily.NetSec, "СетьДиагностика"),
            ["net.availability"] = new("net.availability", "8. Тестирование доступности", "Контроль доступности сервиса по интервалу в рамках прогона.", TestFamily.NetSec, "СетьДоступность"),
            ["net.security"] = new("net.security", "7. Тестирование безопасности", "Базовые проверки конфигурации безопасности без атакующих действий.", TestFamily.NetSec, "SecurityBaseline"),
            ["net.preflight"] = new("net.preflight", "1. Дымовое тестирование", "Быстрая предварительная проверка DNS/TCP/TLS/HTTP перед основными тестами.", TestFamily.NetSec, "Preflight")
        });

    public static bool TryGetByModuleId(string moduleId, out ModuleDescriptor descriptor)
        => DescriptorsById.TryGetValue(moduleId, out descriptor!);

    public static string GetSuffix(string moduleId)
    {
        return TryGetByModuleId(moduleId, out var descriptor)
            ? descriptor.ModuleSuffix
            : "Module";
    }
}

/// <summary>
/// Описание модуля из каталога MVP.
/// </summary>
public sealed record ModuleDescriptor(string ModuleId, string DisplayName, string Description, TestFamily Family, string ModuleSuffix);
