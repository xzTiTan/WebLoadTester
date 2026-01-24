using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Presentation.ViewModels;

/// <summary>
/// ViewModel вкладки истории прогонов.
/// </summary>
public partial class RunsTabViewModel : ObservableObject
{
    private readonly IRunStore _runStore;
    private readonly string _runsRoot;
    private readonly Func<string, Task> _repeatRun;

    public RunsTabViewModel(IRunStore runStore, string runsRoot, Func<string, Task> repeatRun)
    {
        _runStore = runStore;
        _runsRoot = runsRoot;
        _repeatRun = repeatRun;
        StatusFilterOptions.Add("Success");
        StatusFilterOptions.Add("Failed");
        StatusFilterOptions.Add("Partial");
        StatusFilterOptions.Add("Cancelled");
    }

    public ObservableCollection<TestRunSummary> Runs { get; } = new();
    public ObservableCollection<string> ModuleFilterOptions { get; } = new();
    public ObservableCollection<string> StatusFilterOptions { get; } = new();

    [ObservableProperty]
    private TestRunSummary? selectedRun;

    [ObservableProperty]
    private string? selectedModuleType;

    [ObservableProperty]
    private string? selectedStatus;

    [ObservableProperty]
    private DateTime? fromDate;

    [ObservableProperty]
    private DateTime? toDate;

    [ObservableProperty]
    private string? searchText;

    public void SetModuleOptions(IEnumerable<string> moduleTypes)
    {
        ModuleFilterOptions.Clear();
        foreach (var moduleType in moduleTypes.OrderBy(m => m))
        {
            ModuleFilterOptions.Add(moduleType);
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var query = new RunQuery
        {
            ModuleType = SelectedModuleType,
            Status = SelectedStatus,
            From = FromDate.HasValue ? new DateTimeOffset(FromDate.Value) : null,
            To = ToDate.HasValue ? new DateTimeOffset(ToDate.Value) : null,
            Search = SearchText
        };

        Runs.Clear();
        var items = await _runStore.QueryRunsAsync(query, CancellationToken.None);
        foreach (var item in items)
        {
            Runs.Add(item);
        }
    }

    [RelayCommand]
    private void OpenJson()
    {
        if (SelectedRun == null)
        {
            return;
        }

        var path = Path.Combine(_runsRoot, SelectedRun.RunId, "report.json");
        OpenPath(path);
    }

    [RelayCommand]
    private void OpenHtml()
    {
        if (SelectedRun == null)
        {
            return;
        }

        var path = Path.Combine(_runsRoot, SelectedRun.RunId, "report.html");
        OpenPath(path);
    }

    [RelayCommand]
    private void OpenRunFolder()
    {
        if (SelectedRun == null)
        {
            return;
        }

        var path = Path.Combine(_runsRoot, SelectedRun.RunId);
        OpenPath(path);
    }

    [RelayCommand]
    private async Task CopyRunIdAsync()
    {
        if (SelectedRun == null)
        {
            return;
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.Clipboard is IClipboard clipboard)
        {
            await clipboard.SetTextAsync(SelectedRun.RunId);
        }
    }

    [RelayCommand]
    private async Task RepeatRunAsync()
    {
        if (SelectedRun == null)
        {
            return;
        }

        await _repeatRun(SelectedRun.RunId);
    }

    private static void OpenPath(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        };
        Process.Start(psi);
    }
}
