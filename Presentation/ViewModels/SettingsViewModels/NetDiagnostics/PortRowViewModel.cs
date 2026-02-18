using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.NetDiagnostics;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels.NetDiagnostics;

public partial class PortRowViewModel : ObservableObject
{
    public PortRowViewModel(DiagnosticPort model)
    {
        Model = model;
        port = model.Port;
        label = model.Protocol;
    }

    public DiagnosticPort Model { get; }

    [ObservableProperty] private int port;
    [ObservableProperty] private string label = string.Empty;

    public bool HasRowError => !string.IsNullOrWhiteSpace(RowErrorText);
    public string RowErrorText => Port is < 1 or > 65535 ? "Порт: допустимый диапазон 1..65535" : string.Empty;

    partial void OnPortChanged(int value)
    {
        Model.Port = value;
        OnPropertyChanged(nameof(RowErrorText));
        OnPropertyChanged(nameof(HasRowError));
    }

    partial void OnLabelChanged(string value)
    {
        Model.Protocol = string.IsNullOrWhiteSpace(value) ? "Tcp" : value;
    }

    public PortRowViewModel Clone() => new(new DiagnosticPort { Port = Port, Protocol = Label });

    public void Clear()
    {
        Port = 443;
        Label = string.Empty;
    }
}
