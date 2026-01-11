using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WebLoadTester.Presentation.ViewModels.Tabs;

public partial class ReportsTabViewModel : ObservableObject
{
    public ReportsTabViewModel(string reportsRoot)
    {
        ReportsRoot = reportsRoot;
        Refresh();
    }

    public string ReportsRoot { get; }
    public ObservableCollection<ReportItemViewModel> Reports { get; } = new();

    [ObservableProperty]
    private ReportItemViewModel? selectedReport;

    partial void OnSelectedReportChanged(ReportItemViewModel? value)
    {
        OpenHtmlCommand.NotifyCanExecuteChanged();
    }

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

    private bool CanOpen() => SelectedReport != null;
}

public class ReportItemViewModel
{
    public ReportItemViewModel(string fullPath)
    {
        FullPath = fullPath;
        FileName = Path.GetFileName(fullPath);
    }

    public string FileName { get; }
    public string FullPath { get; }
}
