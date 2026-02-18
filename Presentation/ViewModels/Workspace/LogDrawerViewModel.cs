using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebLoadTester.Presentation.ViewModels.Controls;

namespace WebLoadTester.Presentation.ViewModels.Workspace;

public partial class LogDrawerViewModel : ObservableObject
{
    private const int MaxLines = 10_000;
    private const int TrimBatchSize = 250;

    private readonly ObservableCollection<LogLineViewModel> _allLines = new();
    private readonly DispatcherTimer _filterDebounceTimer;

    public LogDrawerViewModel()
    {
        _filterDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _filterDebounceTimer.Tick += (_, _) =>
        {
            _filterDebounceTimer.Stop();
            RebuildVisibleLines();
        };
    }

    public ObservableCollection<LogLineViewModel> VisibleLines { get; } = new();

    [ObservableProperty]
    private bool isExpanded;

    [ObservableProperty]
    private bool autoScroll = true;

    [ObservableProperty]
    private bool onlyErrors;

    [ObservableProperty]
    private string filterText = string.Empty;

    public void Append(LogLineViewModel line)
    {
        _allLines.Add(line);
        if (_allLines.Count > MaxLines)
        {
            var removeCount = Math.Min(TrimBatchSize, _allLines.Count - MaxLines);
            for (var i = 0; i < removeCount; i++)
            {
                _allLines.RemoveAt(0);
            }
        }

        if (MatchesFilters(line))
        {
            VisibleLines.Add(line);
            if (VisibleLines.Count > MaxLines)
            {
                var removeCount = Math.Min(TrimBatchSize, VisibleLines.Count - MaxLines);
                for (var i = 0; i < removeCount; i++)
                {
                    VisibleLines.RemoveAt(0);
                }
            }
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task CopyAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        IClipboard? clipboard = desktop.MainWindow?.Clipboard;
        if (clipboard == null)
        {
            return;
        }

        var text = string.Join(Environment.NewLine, VisibleLines.Select(l => l.RenderedText));
        await clipboard.SetTextAsync(text);
    }

    [RelayCommand]
    private void Clear()
    {
        _allLines.Clear();
        VisibleLines.Clear();
    }

    partial void OnOnlyErrorsChanged(bool value) => RebuildVisibleLines();

    partial void OnFilterTextChanged(string value)
    {
        _filterDebounceTimer.Stop();
        _filterDebounceTimer.Start();
    }

    public void RebuildVisibleLines()
    {
        var filtered = _allLines.Where(MatchesFilters).ToList();
        VisibleLines.Clear();
        foreach (var line in filtered)
        {
            VisibleLines.Add(line);
        }
    }

    private bool MatchesFilters(LogLineViewModel line)
    {
        if (OnlyErrors && !string.Equals(line.Level, "ERROR", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(FilterText))
        {
            return true;
        }

        return line.RenderedText.Contains(FilterText, StringComparison.OrdinalIgnoreCase);
    }
}
