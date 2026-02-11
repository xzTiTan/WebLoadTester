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
            ["ui.scenario"] = new("ui.scenario", "A1 UI сценарий", "Сценарные UI шаги с измерениями и скриншотами.", TestFamily.UiTesting, "UIСценарий"),
            ["ui.snapshot"] = new("ui.snapshot", "A2 UI снимки", "Массовые скриншоты списка URL.", TestFamily.UiTesting, "UISнимки"),
            ["ui.timing"] = new("ui.timing", "A3 UI тайминги", "Замеры времени загрузки страниц.", TestFamily.UiTesting, "UITайминги"),
            ["http.functional"] = new("http.functional", "B1 HTTP функциональные", "Функциональные HTTP проверки с ассерциями.", TestFamily.HttpTesting, "HTTPФункциональные"),
            ["http.performance"] = new("http.performance", "B2 HTTP производительность", "Управляемая HTTP-нагрузка (iterations/duration).", TestFamily.HttpTesting, "HTTPПроизводительность"),
            ["http.assets"] = new("http.assets", "B3 HTTP ассеты", "Проверки статических HTTP-ассетов.", TestFamily.HttpTesting, "HTTPАссеты"),
            ["net.diagnostics"] = new("net.diagnostics", "C1 DNS/TCP/TLS диагностика", "Диагностика DNS, TCP и TLS.", TestFamily.NetSec, "СетьДиагностика"),
            ["net.availability"] = new("net.availability", "C2 монитор доступности", "Проверка доступности сервиса по интервалу.", TestFamily.NetSec, "СетьДоступность"),
            ["net.security"] = new("net.security", "C3 security baseline", "Базовые security-проверки без атак.", TestFamily.NetSec, "SecurityBaseline"),
            ["net.preflight"] = new("net.preflight", "C4 preflight", "Быстрый preflight DNS/TCP/TLS/HTTP.", TestFamily.NetSec, "Preflight")
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
