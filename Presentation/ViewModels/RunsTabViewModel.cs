using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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

    [ObservableProperty]
    private string userMessage = string.Empty;

    [ObservableProperty]
    private bool isDeleteConfirmVisible;

    private string? pendingDeleteRunId;

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
        var previousRunId = SelectedRun?.RunId;
        var fromDate = NormalizeDate(FromDate);
        var toDate = NormalizeDate(ToDate);
        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
        {
            (fromDate, toDate) = (toDate, fromDate);
            FromDate = fromDate?.DateTime;
            ToDate = toDate?.DateTime;
        }

        var query = new RunQuery
        {
            ModuleType = SelectedModuleType,
            Status = SelectedStatus,
            From = fromDate,
            To = toDate,
            Search = SearchText
        };

        IsDeleteConfirmVisible = false;
        Runs.Clear();
        var items = await _runStore.QueryRunsAsync(query, CancellationToken.None);
        foreach (var item in items)
        {
            Runs.Add(item);
        }

        if (Runs.Count > 0)
        {
            SelectedRun = Runs.FirstOrDefault(run => run.RunId == previousRunId) ?? Runs[0];
        }

        UserMessage = string.Empty;
    }

    [RelayCommand]
    private void OpenJson()
    {
        if (SelectedRun == null)
        {
            return;
        }

        var path = Path.Combine(_runsRoot, SelectedRun.RunId, "report.json");
        OpenPath(path, "Файл JSON отчёта не найден.");
    }

    [RelayCommand]
    private void OpenHtml()
    {
        if (SelectedRun == null)
        {
            return;
        }

        var path = Path.Combine(_runsRoot, SelectedRun.RunId, "report.html");
        OpenPath(path, "HTML отчёт не найден.");
    }

    [RelayCommand]
    private void OpenRunFolder()
    {
        if (SelectedRun == null)
        {
            return;
        }

        var path = Path.Combine(_runsRoot, SelectedRun.RunId);
        OpenPath(path, "Папка прогона не найдена.");
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
            UserMessage = "RunId скопирован в буфер обмена.";
        }
    }

    [RelayCommand]
    private async Task CopyRunPathAsync()
    {
        if (SelectedRun == null)
        {
            return;
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.Clipboard is IClipboard clipboard)
        {
            var path = Path.Combine(_runsRoot, SelectedRun.RunId);
            await clipboard.SetTextAsync(path);
            UserMessage = "Путь прогона скопирован в буфер обмена.";
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

    [RelayCommand]
    private void RequestDeleteRun()
    {
        if (SelectedRun == null)
        {
            return;
        }

        pendingDeleteRunId = SelectedRun.RunId;
        IsDeleteConfirmVisible = true;
        UserMessage = $"Удалить прогон {SelectedRun.RunId}?";
    }

    [RelayCommand]
    private async Task ConfirmDeleteRunAsync()
    {
        if (pendingDeleteRunId == null)
        {
            return;
        }

        try
        {
            await _runStore.DeleteRunAsync(pendingDeleteRunId, CancellationToken.None);
            var runFolder = Path.Combine(_runsRoot, pendingDeleteRunId);
            if (Directory.Exists(runFolder))
            {
                Directory.Delete(runFolder, recursive: true);
            }

            IsDeleteConfirmVisible = false;
            pendingDeleteRunId = null;
            UserMessage = "Прогон удалён.";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            UserMessage = $"Не удалось удалить прогон: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CancelDeleteRun()
    {
        IsDeleteConfirmVisible = false;
        pendingDeleteRunId = null;
        UserMessage = string.Empty;
    }

    private void OpenPath(string path, string notFoundMessage)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            UserMessage = notFoundMessage;
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        };
        Process.Start(psi);
        UserMessage = string.Empty;
    }

    private static DateTimeOffset? NormalizeDate(DateTime? value)
    {
        if (!value.HasValue || value.Value == DateTime.MinValue)
        {
            return null;
        }

        var date = DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Utc);
        return new DateTimeOffset(date);
    }
}
