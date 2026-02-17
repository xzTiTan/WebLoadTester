using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebLoadTester.Modules.NetDiagnostics;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public partial class NetDiagnosticsSettingsViewModel : SettingsViewModelBase
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
        Ports = new ObservableCollection<PortItem>(settings.Ports.Select(p => new PortItem { Port = p.Port, Protocol = p.Protocol }));
        Ports.CollectionChanged += OnPortsChanged;
        foreach (var port in Ports)
        {
            port.PropertyChanged += OnPortItemChanged;
        }

        if (UseAutoPorts)
        {
            ApplyAutoPorts();
        }
        else
        {
            UpdatePortsSettings();
        }
    }

    public override object Settings => _settings;
    public override string Title => "Сетевая диагностика";

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

        Ports.Clear();
        foreach (var port in s.Ports)
        {
            var item = new PortItem { Port = port.Port, Protocol = port.Protocol };
            item.PropertyChanged += OnPortItemChanged;
            Ports.Add(item);
        }

        SelectedPort = Ports.FirstOrDefault();
        UpdatePortsSettings();
    }

    public ObservableCollection<PortItem> Ports { get; }
    public string[] ProtocolOptions { get; } = { "Tcp" };

    [ObservableProperty] private string hostname = string.Empty;
    [ObservableProperty] private bool useAutoPorts;
    [ObservableProperty] private PortItem? selectedPort;
    [ObservableProperty] private bool checkDns;
    [ObservableProperty] private bool checkTcp;
    [ObservableProperty] private bool checkTls;

    partial void OnSelectedPortChanged(PortItem? value)
    {
        RemoveSelectedPortCommand.NotifyCanExecuteChanged();
        DuplicateSelectedPortCommand.NotifyCanExecuteChanged();
    }

    partial void OnHostnameChanged(string value)
    {
        _settings.Hostname = value;
    }

    partial void OnUseAutoPortsChanged(bool value)
    {
        _settings.UseAutoPorts = value;
        RemoveSelectedPortCommand.NotifyCanExecuteChanged();
        DuplicateSelectedPortCommand.NotifyCanExecuteChanged();

        if (value)
        {
            ApplyAutoPorts();
        }
    }

    partial void OnCheckDnsChanged(bool value) => _settings.CheckDns = value;
    partial void OnCheckTcpChanged(bool value) => _settings.CheckTcp = value;
    partial void OnCheckTlsChanged(bool value) => _settings.CheckTls = value;

    [RelayCommand]
    private void AddPort()
    {
        if (UseAutoPorts)
        {
            return;
        }

        var item = new PortItem { Port = 443, Protocol = "Tcp" };
        item.PropertyChanged += OnPortItemChanged;
        Ports.Add(item);
        SelectedPort = item;
        UpdatePortsSettings();
    }

    [RelayCommand(CanExecute = nameof(CanMutateSelectedPort))]
    private void DuplicateSelectedPort()
    {
        if (UseAutoPorts || SelectedPort == null)
        {
            return;
        }

        var item = new PortItem { Port = SelectedPort.Port, Protocol = SelectedPort.Protocol };
        item.PropertyChanged += OnPortItemChanged;
        var index = Ports.IndexOf(SelectedPort);
        Ports.Insert(index + 1, item);
        SelectedPort = item;
        UpdatePortsSettings();
    }

    [RelayCommand(CanExecute = nameof(CanMutateSelectedPort))]
    private void RemoveSelectedPort()
    {
        if (UseAutoPorts || SelectedPort == null)
        {
            return;
        }

        var index = Ports.IndexOf(SelectedPort);
        Ports.Remove(SelectedPort);
        SelectedPort = index >= 0 && Ports.Count > 0 ? Ports[Math.Min(index, Ports.Count - 1)] : null;
        UpdatePortsSettings();
    }

    private void ApplyAutoPorts()
    {
        Ports.Clear();
        var item = new PortItem { Port = 443, Protocol = "Tcp" };
        item.PropertyChanged += OnPortItemChanged;
        Ports.Add(item);
        SelectedPort = item;
        UpdatePortsSettings();
    }

    private void OnPortsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (PortItem item in e.NewItems)
            {
                item.PropertyChanged += OnPortItemChanged;
            }
        }

        if (e.OldItems != null)
        {
            foreach (PortItem item in e.OldItems)
            {
                item.PropertyChanged -= OnPortItemChanged;
            }
        }

        UpdatePortsSettings();
        OnPropertyChanged(nameof(Ports));
    }

    private void OnPortItemChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PortItem.Port) or nameof(PortItem.Protocol))
        {
            UpdatePortsSettings();
        }
    }

    private bool CanMutateSelectedPort() => !UseAutoPorts && SelectedPort != null;

    private void UpdatePortsSettings()
    {
        _settings.Ports = Ports
            .Select(p => new DiagnosticPort { Port = p.Port, Protocol = string.IsNullOrWhiteSpace(p.Protocol) ? "Tcp" : p.Protocol })
            .ToList();
    }

    public partial class PortItem : ObservableObject
    {
        [ObservableProperty] private int port;
        [ObservableProperty] private string protocol = "Tcp";
    }
}
