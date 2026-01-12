using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebLoadTester.Modules.NetDiagnostics;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

/// <summary>
/// ViewModel настроек сетевой диагностики.
/// </summary>
public partial class NetDiagnosticsSettingsViewModel : SettingsViewModelBase
{
    private readonly NetDiagnosticsSettings _settings;

    /// <summary>
    /// Инициализирует ViewModel и копирует настройки.
    /// </summary>
    public NetDiagnosticsSettingsViewModel(NetDiagnosticsSettings settings)
    {
        _settings = settings;
        hostname = settings.Hostname;
        autoPortsByScheme = settings.AutoPortsByScheme;
        Ports = new ObservableCollection<PortItem>(settings.Ports.Select(port => new PortItem(port)));
        enableDns = settings.EnableDns;
        enableTcp = settings.EnableTcp;
        enableTls = settings.EnableTls;
        Ports.CollectionChanged += OnPortsChanged;
        foreach (var port in Ports)
        {
            port.PropertyChanged += OnPortItemChanged;
        }
        if (AutoPortsByScheme)
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

    public ObservableCollection<PortItem> Ports { get; }

    [ObservableProperty]
    private string hostname = string.Empty;

    [ObservableProperty]
    private bool autoPortsByScheme;

    [ObservableProperty]
    private PortItem? selectedPort;

    [ObservableProperty]
    private bool enableDns;

    [ObservableProperty]
    private bool enableTcp;

    [ObservableProperty]
    private bool enableTls;

    /// <summary>
    /// Синхронизирует имя хоста.
    /// </summary>
    partial void OnHostnameChanged(string value)
    {
        _settings.Hostname = value;
        if (AutoPortsByScheme)
        {
            ApplyAutoPorts();
        }
    }
    /// <summary>
    /// Синхронизирует режим автопортов.
    /// </summary>
    partial void OnAutoPortsBySchemeChanged(bool value)
    {
        _settings.AutoPortsByScheme = value;
        if (value)
        {
            ApplyAutoPorts();
        }
    }
    /// <summary>
    /// Синхронизирует флаг DNS-проверки.
    /// </summary>
    partial void OnEnableDnsChanged(bool value) => _settings.EnableDns = value;
    /// <summary>
    /// Синхронизирует флаг TCP-проверки.
    /// </summary>
    partial void OnEnableTcpChanged(bool value) => _settings.EnableTcp = value;
    /// <summary>
    /// Синхронизирует флаг TLS-проверки.
    /// </summary>
    partial void OnEnableTlsChanged(bool value) => _settings.EnableTls = value;

    [RelayCommand]
    private void AddPort()
    {
        if (AutoPortsByScheme)
        {
            return;
        }

        var port = new PortItem(80);
        port.PropertyChanged += OnPortItemChanged;
        Ports.Add(port);
        SelectedPort = port;
        UpdatePortsSettings();
    }

    [RelayCommand]
    private void RemoveSelectedPort()
    {
        if (AutoPortsByScheme)
        {
            return;
        }

        if (SelectedPort != null)
        {
            Ports.Remove(SelectedPort);
            UpdatePortsSettings();
        }
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
    }

    private void ApplyAutoPorts()
    {
        var ports = GetDefaultPortsFromScheme();
        Ports.Clear();
        foreach (var port in ports)
        {
            Ports.Add(new PortItem(port));
        }
        UpdatePortsSettings();
    }

    private IEnumerable<int> GetDefaultPortsFromScheme()
    {
        if (Uri.TryCreate(Hostname, UriKind.Absolute, out var uri))
        {
            return uri.Scheme.Equals("https", System.StringComparison.OrdinalIgnoreCase)
                ? new[] { 443 }
                : new[] { 80 };
        }

        return new[] { 80, 443 };
    }

    private void UpdatePortsSettings()
    {
        var normalized = Ports.Select(port => port.Value)
            .Where(port => port is >= 1 and <= 65535)
            .Distinct()
            .ToList();
        _settings.Ports = normalized;

        if (!AutoPortsByScheme)
        {
            var snapshot = Ports.Select(port => port.Value).ToList();
            if (!snapshot.SequenceEqual(normalized))
            {
                Ports.Clear();
                foreach (var port in normalized)
                {
                    Ports.Add(new PortItem(port));
                }
            }
        }
    }

    public partial class PortItem : ObservableObject
    {
        public PortItem(int value) => this.value = value;

        [ObservableProperty]
        private int value;
    }

    private void OnPortItemChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PortItem.Value))
        {
            UpdatePortsSettings();
        }
    }
}
