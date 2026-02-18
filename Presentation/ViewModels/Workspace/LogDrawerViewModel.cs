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
using WebLoadTester.Infrastructure.Storage;
using WebLoadTester.Presentation.ViewModels.Controls;

namespace WebLoadTester.Presentation.ViewModels.Workspace;

public partial class LogDrawerViewModel : ObservableObject
{
    private const int MaxLines = 10_000;
    private const int TrimBatchSize = 250;

    private readonly ObservableCollection<LogLineViewModel> _allLines = new();
    private readonly DispatcherTimer _filterDebounceTimer;
    private readonly Action? _onStateChanged;

    public LogDrawerViewModel(UiLayoutState? initialState = null, Action? onStateChanged = null)
    {
        _onStateChanged = onStateChanged;
        _filterDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _filterDebounceTimer.Tick += (_, _) =>
        {
            _filterDebounceTimer.Stop();
            RebuildVisibleLines();
            _onStateChanged?.Invoke();
        };

        if (initialState != null)
        {
            isExpanded = initialState.IsLogExpanded;
            onlyErrors = initialState.IsLogOnlyErrors;
            filterText = initialState.LogFilterText ?? string.Empty;
        }
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


    public void ShowInLog(string moduleId)
    {
        IsExpanded = true;
        if (!string.IsNullOrWhiteSpace(moduleId))
        {
            FilterText = moduleId;
        }

        _filterDebounceTimer.Stop();
        RebuildVisibleLines();
    }

    public string LastErrorLine(string? moduleId = null)
    {
        for (var i = _allLines.Count - 1; i >= 0; i--)
        {
            var line = _allLines[i];
            if (!string.Equals(line.Level, "ERROR", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(moduleId) ||
                line.ModuleId.Contains(moduleId, StringComparison.OrdinalIgnoreCase) ||
                line.RenderedText.Contains(moduleId, StringComparison.OrdinalIgnoreCase))
            {
                return line.RenderedText;
            }
        }

        return string.Empty;
    }

    public string GetLastErrorShort(string? moduleId = null)
    {
        var line = LastErrorLine(moduleId);
        if (string.IsNullOrWhiteSpace(line))
        {
            return string.Empty;
        }

        return line.Length > 220 ? line[..220] + "…" : line;
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

    partial void OnIsExpandedChanged(bool value) => _onStateChanged?.Invoke();

    partial void OnOnlyErrorsChanged(bool value)
    {
        RebuildVisibleLines();
        _onStateChanged?.Invoke();
    }

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
