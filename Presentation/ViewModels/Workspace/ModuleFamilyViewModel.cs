using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Infrastructure.Storage;
using LegacyFamilyViewModel = WebLoadTester.Presentation.ViewModels.Tabs.ModuleFamilyViewModel;

namespace WebLoadTester.Presentation.ViewModels.Workspace;

public partial class ModuleFamilyViewModel : ObservableObject
{
    private readonly MainWindowViewModel _backend;
    private readonly LegacyFamilyViewModel _legacyFamily;
    private readonly List<ModuleDescriptorVm> _allModules;
    private ModuleDescriptorVm? _lastConfirmedSelection;
    private bool _isSyncingSelection;

    public ModuleFamilyViewModel(string title, MainWindowViewModel backend, LegacyFamilyViewModel legacyFamily, LogDrawerViewModel logDrawer, UiLayoutState? initialLayoutState = null, Action? onLayoutStateChanged = null)
    {
        Title = title;
        _backend = backend;
        _legacyFamily = legacyFamily;

        _allModules = _legacyFamily.Modules
            .Select(m => new ModuleDescriptorVm(m))
            .ToList();

        Modules = new ObservableCollection<ModuleDescriptorVm>(_allModules);

        var selectedLegacy = _legacyFamily.SelectedModule;
        var selectedDescriptor = selectedLegacy == null
            ? Modules.FirstOrDefault()
            : Modules.FirstOrDefault(m => ReferenceEquals(m.Backend, selectedLegacy)) ?? Modules.FirstOrDefault();

        selectedModule = selectedDescriptor;
        _lastConfirmedSelection = selectedDescriptor;

        Workspace = new ModuleWorkspaceViewModel(_backend, logDrawer, initialLayoutState, onLayoutStateChanged);
        UpdateWorkspaceFromSelection(selectedDescriptor);

        _legacyFamily.PropertyChanged += OnLegacyFamilyPropertyChanged;
    }

    public string Title { get; }
    public ObservableCollection<ModuleDescriptorVm> Modules { get; }
    public ModuleWorkspaceViewModel Workspace { get; }

    [ObservableProperty]
    private string moduleSearchText = string.Empty;

    [ObservableProperty]
    private ModuleDescriptorVm? selectedModule;

    partial void OnModuleSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedModuleChanged(ModuleDescriptorVm? value)
    {
        if (_isSyncingSelection || value == null)
        {
            return;
        }

        if (_backend.IsRunning)
        {
            _backend.LogNavigationGuard($"[NavGuard] Workspace module change blocked while running: requested={value.ModuleId}, current={_lastConfirmedSelection?.ModuleId ?? "n/a"}");
            _isSyncingSelection = true;
            SelectedModule = _lastConfirmedSelection;
            _isSyncingSelection = false;
            return;
        }

        _lastConfirmedSelection = value;
        UpdateWorkspaceFromSelection(value);

        if (!ReferenceEquals(_legacyFamily.SelectedModule, value.Backend))
        {
            _legacyFamily.SelectedModule = value.Backend;
        }
    }

    private void ApplyFilter()
    {
        var previous = SelectedModule;

        var filtered = string.IsNullOrWhiteSpace(ModuleSearchText)
            ? _allModules
            : _allModules.Where(m =>
                m.DisplayName.Contains(ModuleSearchText, StringComparison.OrdinalIgnoreCase) ||
                m.ModuleId.Contains(ModuleSearchText, StringComparison.OrdinalIgnoreCase))
                .ToList();

        Modules.Clear();
        foreach (var module in filtered)
        {
            Modules.Add(module);
        }

        _isSyncingSelection = true;
        SelectedModule = previous != null && Modules.Contains(previous)
            ? previous
            : Modules.FirstOrDefault();
        _isSyncingSelection = false;

        if (SelectedModule != null)
        {
            _lastConfirmedSelection = SelectedModule;
            UpdateWorkspaceFromSelection(SelectedModule);
        }
    }

    private void OnLegacyFamilyPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LegacyFamilyViewModel.SelectedModule))
        {
            return;
        }

        var target = _legacyFamily.SelectedModule == null
            ? null
            : _allModules.FirstOrDefault(m => ReferenceEquals(m.Backend, _legacyFamily.SelectedModule));

        if (target != null && !ReferenceEquals(SelectedModule, target))
        {
            _isSyncingSelection = true;
            SelectedModule = target;
            _isSyncingSelection = false;
            _lastConfirmedSelection = target;
            UpdateWorkspaceFromSelection(target);
        }
    }

    private void UpdateWorkspaceFromSelection(ModuleDescriptorVm? descriptor)
    {
        Workspace.SetSelectedModule(descriptor);
    }
}

public class ModuleDescriptorVm
{
    public ModuleDescriptorVm(ModuleItemViewModel backend)
    {
        Backend = backend;
        DisplayName = backend.DisplayName;
        Description = backend.Description;
        ModuleId = backend.Module.Id;
        ModuleSettingsVm = backend.SettingsViewModel;
        ModuleConfig = backend.ModuleConfig;
    }

    public ModuleItemViewModel Backend { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public string ModuleId { get; }
    public object ModuleSettingsVm { get; }
    public ModuleConfigViewModel ModuleConfig { get; }
}
