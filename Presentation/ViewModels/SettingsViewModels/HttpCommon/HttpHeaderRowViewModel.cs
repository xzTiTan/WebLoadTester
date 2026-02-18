using CommunityToolkit.Mvvm.ComponentModel;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels.HttpCommon;

public partial class HttpHeaderRowViewModel : ObservableObject
{
    [ObservableProperty] private string key = string.Empty;
    [ObservableProperty] private string value = string.Empty;

    public bool HasRowError => !string.IsNullOrWhiteSpace(RowErrorText);

    public string RowErrorText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Key) || string.IsNullOrWhiteSpace(Value))
            {
                return "Header: Key/Value обязателен";
            }

            return string.Empty;
        }
    }

    partial void OnKeyChanged(string value)
    {
        OnPropertyChanged(nameof(RowErrorText));
        OnPropertyChanged(nameof(HasRowError));
    }

    partial void OnValueChanged(string value)
    {
        OnPropertyChanged(nameof(RowErrorText));
        OnPropertyChanged(nameof(HasRowError));
    }

    public HttpHeaderRowViewModel Clone() => new() { Key = Key, Value = Value };

    public void Clear()
    {
        Key = string.Empty;
        Value = string.Empty;
    }

    public string ToHeader() => $"{Key}:{Value}";

    public static HttpHeaderRowViewModel FromHeader(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new HttpHeaderRowViewModel();
        }

        var idx = raw.IndexOf(':');
        if (idx < 0)
        {
            return new HttpHeaderRowViewModel { Key = raw.Trim(), Value = string.Empty };
        }

        return new HttpHeaderRowViewModel
        {
            Key = raw[..idx].Trim(),
            Value = raw[(idx + 1)..].Trim()
        };
    }
}
