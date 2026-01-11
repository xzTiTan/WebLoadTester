using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebLoadTester.Presentation.ViewModels;

namespace WebLoadTester.Presentation.ViewModels.Tabs;

public sealed partial class ReportsTabViewModel : ViewModelBase
{
    public ReportsTabViewModel(string reportsRoot)
    {
        ReportsRoot = reportsRoot;
        LoadReports();
        OpenReportCommand = new RelayCommand(OpenReport, () => !string.IsNullOrWhiteSpace(SelectedReport));
        OpenFolderCommand = new RelayCommand(OpenFolder);
    }

    public string ReportsRoot { get; }
    public ObservableCollection<string> Reports { get; } = new();

    [ObservableProperty]
    private string? _selectedReport;

    public IRelayCommand OpenReportCommand { get; }
    public IRelayCommand OpenFolderCommand { get; }

    public void LoadReports()
    {
        Reports.Clear();
        if (!Directory.Exists(ReportsRoot))
        {
            return;
        }
        foreach (var file in Directory.GetFiles(ReportsRoot, "*.html").OrderByDescending(f => f))
        {
            Reports.Add(file);
        }
        SelectedReport = Reports.FirstOrDefault();
    }

    private void OpenReport()
    {
        if (string.IsNullOrWhiteSpace(SelectedReport))
        {
            return;
        }
        Process.Start(new ProcessStartInfo(SelectedReport) { UseShellExecute = true });
    }

    private void OpenFolder()
    {
        Process.Start(new ProcessStartInfo(ReportsRoot) { UseShellExecute = true });
    }

    partial void OnSelectedReportChanged(string? value)
    {
        OpenReportCommand.NotifyCanExecuteChanged();
    }
}
