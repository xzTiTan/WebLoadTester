using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Presentation.ViewModels.SettingsViewModels;

namespace WebLoadTester.Presentation.ViewModels;

/// <summary>
/// Представление модуля и его настроек для списка в UI.
/// </summary>
public partial class ModuleItemViewModel : ObservableObject
{
    /// <summary>
    /// Создаёт ViewModel элемента модуля.
    /// </summary>
    public ModuleItemViewModel(ITestModule module, SettingsViewModelBase settingsViewModel, ModuleConfigViewModel moduleConfig)
    {
        Module = module;
        SettingsViewModel = settingsViewModel;
        ModuleConfig = moduleConfig;
    }

    public ITestModule Module { get; }
    public SettingsViewModelBase SettingsViewModel { get; }
    public ModuleConfigViewModel ModuleConfig { get; }
    /// <summary>
    /// Отображаемое имя модуля.
    /// </summary>
    public string DisplayName => Module.DisplayName;
    public string Description => Module.Description;

    [ObservableProperty]
    private TestReport? lastReport;

    public ObservableCollection<ResultBase> DisplayResults { get; } = new();

    public ObservableCollection<ArtifactListItem> ArtifactItems { get; } = new();

    public bool HasReport => LastReport != null;

    partial void OnLastReportChanged(TestReport? value)
    {
        OnPropertyChanged(nameof(HasReport));
        RefreshDisplayResults(value);
        RefreshArtifactItems(value);
    }

    private void RefreshDisplayResults(TestReport? report)
    {
        DisplayResults.Clear();
        if (report == null)
        {
            return;
        }

        foreach (var item in report.Results.Take(2000))
        {
            DisplayResults.Add(item);
        }
    }

    private void RefreshArtifactItems(TestReport? report)
    {
        ArtifactItems.Clear();
        if (report == null)
        {
            return;
        }

        var seenPaths = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        AddArtifact("report.json", ResolveArtifactPath(report.Artifacts.JsonPath, "report.json"), report.RunId, seenPaths);
        if (!string.IsNullOrWhiteSpace(report.Artifacts.HtmlPath))
        {
            AddArtifact("report.html", ResolveArtifactPath(report.Artifacts.HtmlPath, "report.html"), report.RunId, seenPaths);
        }

        AddArtifact("logs/run.log", ResolveArtifactPath(report.Artifacts.LogPath, "logs/run.log"), report.RunId, seenPaths);

        foreach (var screenshot in report.Results
                     .Select(result => result switch
                     {
                         RunResult run => run.ScreenshotPath,
                         StepResult step => step.ScreenshotPath,
                         _ => null
                     })
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Distinct())
        {
            AddArtifact(screenshot!, screenshot!, report.RunId, seenPaths);
        }

        foreach (var artifact in report.ModuleArtifacts.Where(a => !string.IsNullOrWhiteSpace(a.RelativePath)))
        {
            AddArtifact(artifact.RelativePath, artifact.RelativePath, report.RunId, seenPaths);
        }
    }

    private void AddArtifact(string name, string relativePath, string runId, ISet<string> seenPaths)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || !seenPaths.Add(relativePath))
        {
            return;
        }

        ArtifactItems.Add(new ArtifactListItem(name, relativePath, runId));
    }

    private static string ResolveArtifactPath(string? path, string fallback)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return fallback;
        }

        var normalizedPath = path.Replace('\\', '/');
        var normalizedFallback = fallback.Replace('\\', '/');
        return normalizedPath.EndsWith(normalizedFallback, System.StringComparison.OrdinalIgnoreCase)
            ? normalizedFallback
            : path;
    }
}

public record ArtifactListItem(string Name, string RelativePath, string RunId);
