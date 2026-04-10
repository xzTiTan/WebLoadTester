using System;
using System.Collections.Generic;
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
        name = model.Name;
        method = model.Method;
        url = model.Path;
        expectedStatus = model.ExpectedStatusCode ?? 200;
        bodyContains = model.BodyContains ?? string.Empty;
        jsonFieldEqualsText = model.JsonFieldEqualsText;
    }

    public HttpFunctionalEndpoint Model { get; }

    public string[] Methods { get; } = HttpFunctionalEndpoint.MethodOptions.ToArray();

    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string method = "GET";
    [ObservableProperty] private string url = "/";
    [ObservableProperty] private int expectedStatus = 200;
    [ObservableProperty] private string bodyContains = string.Empty;
    [ObservableProperty] private string jsonFieldEqualsText = string.Empty;

    public bool HasRowError => !string.IsNullOrWhiteSpace(RowErrorText);
    public string RowErrorText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                return "Endpoint: name is required";
            }

            if (string.IsNullOrWhiteSpace(Url))
            {
                return "Request path is required";
            }

            if (HasInvalidJsonFieldEqualsRule(JsonFieldEqualsText))
            {
                return "JsonFieldEquals must use path=value;path2=value2";
            }

            return string.Empty;
        }
    }

    partial void OnNameChanged(string value)
    {
        var normalized = InputValueGuard.NormalizeOptionalText(value);
        if (!string.Equals(normalized, value, StringComparison.Ordinal))
        {
            Name = normalized;
            return;
        }

        Model.Name = normalized;
        OnPropertyChanged(nameof(RowErrorText));
        OnPropertyChanged(nameof(HasRowError));
    }

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

    partial void OnBodyContainsChanged(string value)
    {
        var normalized = InputValueGuard.NormalizeOptionalText(value);
        if (!string.Equals(normalized, value, StringComparison.Ordinal))
        {
            BodyContains = normalized;
            return;
        }

        Model.BodyContains = normalized;
    }

    partial void OnJsonFieldEqualsTextChanged(string value)
    {
        var normalized = InputValueGuard.NormalizeOptionalText(value);
        if (!string.Equals(normalized, value, StringComparison.Ordinal))
        {
            JsonFieldEqualsText = normalized;
            return;
        }

        Model.JsonFieldEquals = ParseRules(normalized);
        OnPropertyChanged(nameof(RowErrorText));
        OnPropertyChanged(nameof(HasRowError));
    }

    public HttpFunctionalEndpointRowViewModel Clone()
    {
        return new(new HttpFunctionalEndpoint
        {
            Name = Name,
            Method = Method,
            Path = Url,
            ExpectedStatusCode = ExpectedStatus,
            BodyContains = BodyContains,
            JsonFieldEquals = Model.JsonFieldEquals.ToList(),
            RequiredHeaders = Model.RequiredHeaders.ToList()
        });
    }

    public void Clear()
    {
        Name = string.Empty;
        Method = "GET";
        Url = string.Empty;
        ExpectedStatus = 200;
        BodyContains = string.Empty;
        JsonFieldEqualsText = string.Empty;
    }

    private static List<string> ParseRules(string? raw)
    {
        return string.IsNullOrWhiteSpace(raw)
            ? new List<string>()
            : raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    private static bool HasInvalidJsonFieldEqualsRule(string? raw)
    {
        return ParseRules(raw).Any(rule => !rule.Contains('='));
    }
}
