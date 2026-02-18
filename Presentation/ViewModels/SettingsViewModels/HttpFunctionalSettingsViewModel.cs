using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.HttpFunctional;
using WebLoadTester.Presentation.ViewModels.Controls;
using WebLoadTester.Presentation.Common;
using WebLoadTester.Presentation.ViewModels.SettingsViewModels.HttpCommon;
using WebLoadTester.Presentation.ViewModels.SettingsViewModels.HttpFunctional;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public partial class HttpFunctionalSettingsViewModel : SettingsViewModelBase, IValidatable
{
    private readonly HttpFunctionalSettings _settings;

    public HttpFunctionalSettingsViewModel(HttpFunctionalSettings settings)
    {
        _settings = settings;
        foreach (var endpoint in settings.Endpoints)
        {
            endpoint.NormalizeLegacy();
        }

        baseUrl = settings.BaseUrl;
        timeoutSeconds = settings.TimeoutSeconds;

        EndpointRows = new ObservableCollection<HttpFunctionalEndpointRowViewModel>(settings.Endpoints.Select(CreateEndpointRow));
        if (EndpointRows.Count == 0)
        {
            EndpointRows.Add(CreateEndpointRow(new HttpFunctionalEndpoint
            {
                Name = "Endpoint",
                Method = "GET",
                Path = "/",
                ExpectedStatusCode = 200
            }));
        }

        HeaderRows = new ObservableCollection<HttpHeaderRowViewModel>(BuildCommonHeaders(settings.Endpoints).Select(CreateHeaderRow));

        EndpointsEditor = new RowListEditorViewModel();
        EndpointsEditor.Configure(AddEndpointInternal, RemoveEndpointInternal, MoveEndpointUpInternal, MoveEndpointDownInternal, DuplicateEndpointInternal, GetEndpointErrors,
            selectedItemChanged: item => SelectedEndpointRow = item as HttpFunctionalEndpointRowViewModel);
        EndpointsEditor.SetItems(EndpointRows.Cast<object>());

        HeadersEditor = new RowListEditorViewModel();
        HeadersEditor.Configure(AddHeaderInternal, RemoveHeaderInternal, MoveHeaderUpInternal, MoveHeaderDownInternal, DuplicateHeaderInternal, GetHeaderErrors,
            selectedItemChanged: item => SelectedHeaderRow = item as HttpHeaderRowViewModel);
        HeadersEditor.SetItems(HeaderRows.Cast<object>());

        SelectedEndpointRow = EndpointRows.FirstOrDefault();
        SelectedHeaderRow = HeaderRows.FirstOrDefault();
    }

    public override object Settings => _settings;
    public override string Title => "HTTP функциональные проверки";

    public ObservableCollection<HttpFunctionalEndpointRowViewModel> EndpointRows { get; }
    public ObservableCollection<HttpHeaderRowViewModel> HeaderRows { get; }

    public RowListEditorViewModel EndpointsEditor { get; }
    public RowListEditorViewModel HeadersEditor { get; }

    [ObservableProperty] private HttpFunctionalEndpointRowViewModel? selectedEndpointRow;
    [ObservableProperty] private HttpHeaderRowViewModel? selectedHeaderRow;
    [ObservableProperty] private string baseUrl = string.Empty;
    [ObservableProperty] private int timeoutSeconds;

    partial void OnSelectedEndpointRowChanged(HttpFunctionalEndpointRowViewModel? value)
    {
        EndpointsEditor.SelectedItem = value;
        EndpointsEditor.RaiseCommandState();
        EndpointsEditor.NotifyValidationChanged();
    }

    partial void OnSelectedHeaderRowChanged(HttpHeaderRowViewModel? value)
    {
        HeadersEditor.SelectedItem = value;
        HeadersEditor.RaiseCommandState();
        HeadersEditor.NotifyValidationChanged();
    }

    partial void OnBaseUrlChanged(string value) => _settings.BaseUrl = value;
    partial void OnTimeoutSecondsChanged(int value) => _settings.TimeoutSeconds = value;

    public override void UpdateFrom(object settings)
    {
        if (settings is not HttpFunctionalSettings s)
        {
            return;
        }

        foreach (var endpoint in s.Endpoints)
        {
            endpoint.NormalizeLegacy();
        }

        BaseUrl = s.BaseUrl;
        TimeoutSeconds = s.TimeoutSeconds;

        EndpointRows.Clear();
        foreach (var endpoint in s.Endpoints)
        {
            EndpointRows.Add(CreateEndpointRow(endpoint));
        }

        if (EndpointRows.Count == 0)
        {
            EndpointRows.Add(CreateEndpointRow(new HttpFunctionalEndpoint
            {
                Name = "Endpoint",
                Method = "GET",
                Path = "/",
                ExpectedStatusCode = 200
            }));
        }

        HeaderRows.Clear();
        foreach (var header in BuildCommonHeaders(s.Endpoints))
        {
            HeaderRows.Add(CreateHeaderRow(header));
        }

        EndpointsEditor.SetItems(EndpointRows.Cast<object>());
        HeadersEditor.SetItems(HeaderRows.Cast<object>());
        SelectedEndpointRow = EndpointRows.FirstOrDefault();
        SelectedHeaderRow = HeaderRows.FirstOrDefault();
        SyncAll();
    }

    private object? AddEndpointInternal()
    {
        var row = CreateEndpointRow(new HttpFunctionalEndpoint
        {
            Name = "Endpoint",
            Method = "GET",
            Path = "/",
            ExpectedStatusCode = 200
        });

        var insertIndex = SelectedEndpointRow != null ? EndpointRows.IndexOf(SelectedEndpointRow) + 1 : EndpointRows.Count;
        if (insertIndex < 0 || insertIndex > EndpointRows.Count)
        {
            insertIndex = EndpointRows.Count;
        }

        EndpointRows.Insert(insertIndex, row);
        SelectedEndpointRow = row;
        SyncAll();
        return row;
    }

    private void RemoveEndpointInternal(object? selected)
    {
        if (selected is not HttpFunctionalEndpointRowViewModel row)
        {
            return;
        }

        if (EndpointRows.Count <= 1)
        {
            row.Clear();
            SyncAll();
            return;
        }

        var index = EndpointRows.IndexOf(row);
        if (index < 0)
        {
            return;
        }

        EndpointRows.RemoveAt(index);
        SelectedEndpointRow = EndpointRows.Count > 0 ? EndpointRows[Math.Min(index, EndpointRows.Count - 1)] : null;
        SyncAll();
    }

    private void MoveEndpointUpInternal(object? selected)
    {
        if (selected is not HttpFunctionalEndpointRowViewModel row)
        {
            return;
        }

        var index = EndpointRows.IndexOf(row);
        if (index > 0)
        {
            EndpointRows.Move(index, index - 1);
            SelectedEndpointRow = EndpointRows[index - 1];
            SyncAll();
        }
    }

    private void MoveEndpointDownInternal(object? selected)
    {
        if (selected is not HttpFunctionalEndpointRowViewModel row)
        {
            return;
        }

        var index = EndpointRows.IndexOf(row);
        if (index >= 0 && index < EndpointRows.Count - 1)
        {
            EndpointRows.Move(index, index + 1);
            SelectedEndpointRow = EndpointRows[index + 1];
            SyncAll();
        }
    }

    private void DuplicateEndpointInternal(object? selected)
    {
        if (selected is not HttpFunctionalEndpointRowViewModel row)
        {
            return;
        }

        var clone = row.Clone();
        var index = EndpointRows.IndexOf(row);
        EndpointRows.Insert(index + 1, clone);
        SelectedEndpointRow = clone;
        SyncAll();
    }

    private object? AddHeaderInternal()
    {
        var row = CreateHeaderRow(new HttpHeaderRowViewModel());
        var insertIndex = SelectedHeaderRow != null ? HeaderRows.IndexOf(SelectedHeaderRow) + 1 : HeaderRows.Count;
        if (insertIndex < 0 || insertIndex > HeaderRows.Count)
        {
            insertIndex = HeaderRows.Count;
        }

        HeaderRows.Insert(insertIndex, row);
        SelectedHeaderRow = row;
        SyncAll();
        return row;
    }

    private void RemoveHeaderInternal(object? selected)
    {
        if (selected is not HttpHeaderRowViewModel row)
        {
            return;
        }

        if (HeaderRows.Count <= 1)
        {
            row.Clear();
            SyncAll();
            return;
        }

        var index = HeaderRows.IndexOf(row);
        if (index < 0)
        {
            return;
        }

        HeaderRows.RemoveAt(index);
        SelectedHeaderRow = HeaderRows.Count > 0 ? HeaderRows[Math.Min(index, HeaderRows.Count - 1)] : null;
        SyncAll();
    }

    private void MoveHeaderUpInternal(object? selected)
    {
        if (selected is not HttpHeaderRowViewModel row)
        {
            return;
        }

        var index = HeaderRows.IndexOf(row);
        if (index > 0)
        {
            HeaderRows.Move(index, index - 1);
            SelectedHeaderRow = HeaderRows[index - 1];
            SyncAll();
        }
    }

    private void MoveHeaderDownInternal(object? selected)
    {
        if (selected is not HttpHeaderRowViewModel row)
        {
            return;
        }

        var index = HeaderRows.IndexOf(row);
        if (index >= 0 && index < HeaderRows.Count - 1)
        {
            HeaderRows.Move(index, index + 1);
            SelectedHeaderRow = HeaderRows[index + 1];
            SyncAll();
        }
    }

    private void DuplicateHeaderInternal(object? selected)
    {
        if (selected is not HttpHeaderRowViewModel row)
        {
            return;
        }

        var clone = row.Clone();
        var index = HeaderRows.IndexOf(row);
        HeaderRows.Insert(index + 1, clone);
        SelectedHeaderRow = clone;
        SyncAll();
    }


    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(BaseUrl))
        {
            errors.Add("BaseUrl обязателен.");
        }

        if (TimeoutSeconds < 1)
        {
            errors.Add("TimeoutSeconds должен быть >= 1.");
        }

        errors.AddRange(GetEndpointErrors());
        errors.AddRange(GetHeaderErrors());

        return errors
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();
    }

    private IEnumerable<string> GetEndpointErrors() => EndpointRows.Select(r => r.RowErrorText).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct();
    private IEnumerable<string> GetHeaderErrors() => HeaderRows.Select(r => r.RowErrorText).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct();

    private HttpFunctionalEndpointRowViewModel CreateEndpointRow(HttpFunctionalEndpoint endpoint)
    {
        var row = new HttpFunctionalEndpointRowViewModel(endpoint);
        row.PropertyChanged += (_, _) => SyncAll();
        return row;
    }

    private HttpHeaderRowViewModel CreateHeaderRow(HttpHeaderRowViewModel row)
    {
        row.PropertyChanged += (_, _) => SyncAll();
        return row;
    }

    private static IEnumerable<HttpHeaderRowViewModel> BuildCommonHeaders(IEnumerable<HttpFunctionalEndpoint> endpoints)
    {
        var firstWithHeaders = endpoints.FirstOrDefault(e => e.RequiredHeaders.Count > 0);
        if (firstWithHeaders == null)
        {
            return [new HttpHeaderRowViewModel()];
        }

        return firstWithHeaders.RequiredHeaders.Select(HttpHeaderRowViewModel.FromHeader);
    }

    private void SyncAll()
    {
        _settings.Endpoints = EndpointRows.Select(r => r.Model).ToList();

        var headers = HeaderRows
            .Where(h => !string.IsNullOrWhiteSpace(h.Key) && !string.IsNullOrWhiteSpace(h.Value))
            .Select(h => h.ToHeader())
            .ToList();

        foreach (var endpoint in _settings.Endpoints)
        {
            endpoint.RequiredHeaders = headers.ToList();
        }

        EndpointsEditor.SetItems(EndpointRows.Cast<object>());
        EndpointsEditor.NotifyValidationChanged();
        EndpointsEditor.RaiseCommandState();

        HeadersEditor.SetItems(HeaderRows.Cast<object>());
        HeadersEditor.NotifyValidationChanged();
        HeadersEditor.RaiseCommandState();
        OnPropertyChanged(nameof(Settings));
    }
}
