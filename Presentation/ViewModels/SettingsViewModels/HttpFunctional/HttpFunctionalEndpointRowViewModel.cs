using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.HttpFunctional;
using WebLoadTester.Presentation.Common;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels.HttpFunctional;

public partial class HttpFunctionalEndpointRowViewModel : ObservableObject
{
    public HttpFunctionalEndpointRowViewModel(HttpFunctionalEndpoint model)
    {
        Model = model;
        method = model.Method;
        url = model.Path;
        expectedStatus = model.ExpectedStatusCode ?? 200;
    }

    public HttpFunctionalEndpoint Model { get; }

    public string[] Methods { get; } = { "GET", "POST", "PUT", "DELETE", "PATCH" };

    [ObservableProperty] private string method = "GET";
    [ObservableProperty] private string url = "/";
    [ObservableProperty] private int expectedStatus = 200;

    public bool HasRowError => !string.IsNullOrWhiteSpace(RowErrorText);
    public string RowErrorText => string.IsNullOrWhiteSpace(Url) ? "Точка запроса: адрес обязателен" : string.Empty;

    partial void OnMethodChanged(string value)
    {
        Model.Method = string.IsNullOrWhiteSpace(value) ? "GET" : value;
    }

    partial void OnUrlChanged(string value)
    {
        var normalized = InputValueGuard.NormalizeOptionalText(value);
        if (!string.Equals(normalized, value, StringComparison.Ordinal))
        {
            Url = normalized;
            return;
        }

        Model.Path = normalized;
        OnPropertyChanged(nameof(RowErrorText));
        OnPropertyChanged(nameof(HasRowError));
    }

    partial void OnExpectedStatusChanged(int value)
    {
        var normalized = InputValueGuard.NormalizeIntInRange(value, 100, 599, 200);
        if (normalized != value)
        {
            ExpectedStatus = normalized;
            return;
        }

        Model.ExpectedStatusCode = normalized;
    }

    public HttpFunctionalEndpointRowViewModel Clone()
    {
        return new(new HttpFunctionalEndpoint
        {
            Name = Model.Name,
            Method = Method,
            Path = Url,
            ExpectedStatusCode = ExpectedStatus,
            BodyContains = Model.BodyContains,
            JsonFieldEquals = Model.JsonFieldEquals,
            RequiredHeaders = Model.RequiredHeaders.ToList()
        });
    }

    public void Clear()
    {
        Method = "GET";
        Url = string.Empty;
        ExpectedStatus = 200;
    }
}
