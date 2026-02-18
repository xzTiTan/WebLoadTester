using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.NetDiagnostics;
using WebLoadTester.Presentation.ViewModels.Controls;
using WebLoadTester.Presentation.Common;
using WebLoadTester.Presentation.ViewModels.SettingsViewModels.NetDiagnostics;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public partial class NetDiagnosticsSettingsViewModel : SettingsViewModelBase, IValidatable
{
    private readonly NetDiagnosticsSettings _settings;

    public NetDiagnosticsSettingsViewModel(NetDiagnosticsSettings settings)
    {
        _settings = settings;
        _settings.NormalizeLegacy();

        hostname = settings.Hostname;
        useAutoPorts = settings.UseAutoPorts;
        checkDns = settings.CheckDns;
        checkTcp = settings.CheckTcp;
        checkTls = settings.CheckTls;

        PortRows = new ObservableCollection<PortRowViewModel>(settings.Ports.Select(CreateRow));
        if (PortRows.Count == 0)
        {
            PortRows.Add(CreateRow(new DiagnosticPort { Port = 443, Protocol = "Tcp" }));
        }

        PortsEditor = new RowListEditorViewModel();
        PortsEditor.Configure(AddPortInternal, RemovePortInternal, MovePortUpInternal, MovePortDownInternal, DuplicatePortInternal, GetPortErrors,
            selectedItemChanged: item => SelectedPortRow = item as PortRowViewModel);
        PortsEditor.SetItems(PortRows.Cast<object>());
        SelectedPortRow = PortRows.FirstOrDefault();

        if (UseAutoPorts)
        {
            ApplyAutoPorts();
        }
        else
        {
            SyncPorts();
        }
    }

    public override object Settings => _settings;
    public override string Title => "Сетевая диагностика";

    public ObservableCollection<PortRowViewModel> PortRows { get; }
    public RowListEditorViewModel PortsEditor { get; }

    [ObservableProperty] private string hostname = string.Empty;
    [ObservableProperty] private bool useAutoPorts;
    [ObservableProperty] private PortRowViewModel? selectedPortRow;
    [ObservableProperty] private bool checkDns;
    [ObservableProperty] private bool checkTcp;
    [ObservableProperty] private bool checkTls;

    partial void OnSelectedPortRowChanged(PortRowViewModel? value)
    {
        PortsEditor.SelectedItem = value;
        PortsEditor.RaiseCommandState();
        PortsEditor.NotifyValidationChanged();
    }

    partial void OnHostnameChanged(string value) => _settings.Hostname = value;
    partial void OnCheckDnsChanged(bool value) => _settings.CheckDns = value;
    partial void OnCheckTcpChanged(bool value) => _settings.CheckTcp = value;
    partial void OnCheckTlsChanged(bool value) => _settings.CheckTls = value;

    partial void OnUseAutoPortsChanged(bool value)
    {
        _settings.UseAutoPorts = value;
        if (value)
        {
            ApplyAutoPorts();
            return;
        }

        SyncPorts();
    }

    public override void UpdateFrom(object settings)
    {
        if (settings is not NetDiagnosticsSettings s)
        {
            return;
        }

        s.NormalizeLegacy();

        Hostname = s.Hostname;
        UseAutoPorts = s.UseAutoPorts;
        CheckDns = s.CheckDns;
        CheckTcp = s.CheckTcp;
        CheckTls = s.CheckTls;

        PortRows.Clear();
        foreach (var port in s.Ports)
        {
            PortRows.Add(CreateRow(port));
        }

        if (PortRows.Count == 0)
        {
            PortRows.Add(CreateRow(new DiagnosticPort { Port = 443, Protocol = "Tcp" }));
        }

        PortsEditor.SetItems(PortRows.Cast<object>());
        SelectedPortRow = PortRows.FirstOrDefault();

        if (UseAutoPorts)
        {
            ApplyAutoPorts();
        }
        else
        {
            SyncPorts();
        }
    }

    private object? AddPortInternal()
    {
        if (UseAutoPorts)
        {
            return null;
        }

        var row = CreateRow(new DiagnosticPort { Port = 443, Protocol = "Tcp" });
        var insertIndex = SelectedPortRow != null ? PortRows.IndexOf(SelectedPortRow) + 1 : PortRows.Count;
        if (insertIndex < 0 || insertIndex > PortRows.Count)
        {
            insertIndex = PortRows.Count;
        }

        PortRows.Insert(insertIndex, row);
        SelectedPortRow = row;
        SyncPorts();
        return row;
    }

    private void RemovePortInternal(object? selected)
    {
        if (UseAutoPorts || selected is not PortRowViewModel row)
        {
            return;
        }

        if (PortRows.Count <= 1)
        {
            row.Clear();
            SyncPorts();
            return;
        }

        var index = PortRows.IndexOf(row);
        if (index < 0)
        {
            return;
        }

        PortRows.RemoveAt(index);
        SelectedPortRow = PortRows.Count > 0 ? PortRows[Math.Min(index, PortRows.Count - 1)] : null;
        SyncPorts();
    }

    private void MovePortUpInternal(object? selected)
    {
        if (UseAutoPorts || selected is not PortRowViewModel row)
        {
            return;
        }

        var index = PortRows.IndexOf(row);
        if (index > 0)
        {
            PortRows.Move(index, index - 1);
            SelectedPortRow = PortRows[index - 1];
            SyncPorts();
        }
    }

    private void MovePortDownInternal(object? selected)
    {
        if (UseAutoPorts || selected is not PortRowViewModel row)
        {
            return;
        }

        var index = PortRows.IndexOf(row);
        if (index >= 0 && index < PortRows.Count - 1)
        {
            PortRows.Move(index, index + 1);
            SelectedPortRow = PortRows[index + 1];
            SyncPorts();
        }
    }

    private void DuplicatePortInternal(object? selected)
    {
        if (UseAutoPorts || selected is not PortRowViewModel row)
        {
            return;
        }

        var clone = row.Clone();
        var index = PortRows.IndexOf(row);
        PortRows.Insert(index + 1, clone);
        SelectedPortRow = clone;
        SyncPorts();
    }


    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Hostname))
        {
            errors.Add("Hostname обязателен.");
        }

        if (!UseAutoPorts)
        {
            errors.AddRange(GetPortErrors());
        }

        return errors
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();
    }

    private IEnumerable<string> GetPortErrors() => PortRows.Select(r => r.RowErrorText).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct();

    private PortRowViewModel CreateRow(DiagnosticPort port)
    {
        var row = new PortRowViewModel(port);
        row.PropertyChanged += (_, _) =>
        {
            if (!UseAutoPorts)
            {
                SyncPorts();
            }
        };
        return row;
    }

    private void ApplyAutoPorts()
    {
        PortRows.Clear();
        PortRows.Add(CreateRow(new DiagnosticPort { Port = 443, Protocol = "Tcp" }));
        SelectedPortRow = PortRows.FirstOrDefault();
        SyncPorts();
    }

    private void SyncPorts()
    {
        _settings.Ports = PortRows.Select(r => r.Model).ToList();
        _settings.UseAutoPorts = UseAutoPorts;

        PortsEditor.SetItems(PortRows.Cast<object>());
        PortsEditor.NotifyValidationChanged();
        PortsEditor.RaiseCommandState();
        OnPropertyChanged(nameof(Settings));
    }
}
