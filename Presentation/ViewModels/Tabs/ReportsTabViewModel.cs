using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WebLoadTester.Presentation.ViewModels.Tabs;

/// <summary>
/// ViewModel вкладки отчётов.
/// </summary>
public partial class ReportsTabViewModel : ObservableObject
{
    /// <summary>
    /// Инициализирует вкладку и сразу загружает список отчётов.
    /// </summary>
    public ReportsTabViewModel(string reportsRoot)
    {
        ReportsRoot = reportsRoot;
        Refresh();
    }

    public string ReportsRoot { get; }
    public ObservableCollection<ReportItemViewModel> Reports { get; } = new();

    [ObservableProperty]
    private ReportItemViewModel? selectedReport;

    /// <summary>
    /// Обновляет доступность команды открытия при смене выбора.
    /// </summary>
    partial void OnSelectedReportChanged(ReportItemViewModel? value)
    {
        OpenHtmlCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Перечитывает список HTML-отчётов из папки.
    /// </summary>
    [RelayCommand]
    private void Refresh()
    {
        Reports.Clear();
        if (!Directory.Exists(ReportsRoot))
        {
            return;
        }

        foreach (var file in Directory.GetFiles(ReportsRoot, "report_*.html"))
        {
            Reports.Add(new ReportItemViewModel(file));
        }
    }

    /// <summary>
    /// Открывает выбранный HTML-отчёт в браузере.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanOpen))]
    private void OpenHtml()
    {
        if (SelectedReport == null)
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = SelectedReport.FullPath,
            UseShellExecute = true
        });
    }

    /// <summary>
    /// Открывает папку отчётов в файловом менеджере.
    /// </summary>
    [RelayCommand]
    private void OpenFolder()
    {
        if (!Directory.Exists(ReportsRoot))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = ReportsRoot,
            UseShellExecute = true
        });
    }

    /// <summary>
    /// Проверяет, выбран ли отчёт для открытия.
    /// </summary>
    private bool CanOpen() => SelectedReport != null;
}

/// <summary>
/// ViewModel элемента отчёта.
/// </summary>
public class ReportItemViewModel
{
    /// <summary>
    /// Создаёт элемент отчёта по полному пути.
    /// </summary>
    public ReportItemViewModel(string fullPath)
    {
        FullPath = fullPath;
        FileName = Path.GetFileName(fullPath);
    }

    public string FileName { get; }
    public string FullPath { get; }
}
