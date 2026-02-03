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
    public ModuleItemViewModel(ITestModule module, SettingsViewModelBase settingsViewModel, TestLibraryViewModel testLibrary)
    {
        Module = module;
        SettingsViewModel = settingsViewModel;
        TestLibrary = testLibrary;
    }

    public ITestModule Module { get; }
    public SettingsViewModelBase SettingsViewModel { get; }
    public TestLibraryViewModel TestLibrary { get; }
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

        AddArtifact("report.json", "report.json", report.RunId);
        if (!string.IsNullOrWhiteSpace(report.Artifacts.HtmlPath))
        {
            AddArtifact("report.html", report.Artifacts.HtmlPath, report.RunId);
        }

        AddArtifact("logs/run.log", "logs/run.log", report.RunId);

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
            AddArtifact(screenshot!, screenshot!, report.RunId);
        }

        foreach (var artifact in report.ModuleArtifacts)
        {
            AddArtifact(artifact.RelativePath, artifact.RelativePath, report.RunId);
        }
    }

    private void AddArtifact(string name, string relativePath, string runId)
    {
        ArtifactItems.Add(new ArtifactListItem(name, relativePath, runId));
    }
}

public record ArtifactListItem(string Name, string RelativePath, string RunId);
