using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Presentation.ViewModels.SettingsViewModels;

namespace WebLoadTester.Presentation.ViewModels;

/// <summary>
/// ViewModel управления библиотекой тестов для модуля.
/// </summary>
public partial class TestLibraryViewModel : ObservableObject
{
    private readonly IRunStore _runStore;
    private readonly ITestModule _module;
    private readonly SettingsViewModelBase _settings;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public TestLibraryViewModel(IRunStore runStore, ITestModule module, SettingsViewModelBase settings)
    {
        _runStore = runStore;
        _module = module;
        _settings = settings;
        testName = module.DisplayName;
    }

    public ObservableCollection<TestCaseEntry> TestCases { get; } = new();

    [ObservableProperty]
    private TestCaseEntry? selectedTestCase;

    [ObservableProperty]
    private string testName = string.Empty;

    [ObservableProperty]
    private string testDescription = string.Empty;

    [ObservableProperty]
    private string changeNote = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    partial void OnSelectedTestCaseChanged(TestCaseEntry? value)
    {
        if (value == null)
        {
            return;
        }

        TestName = value.Name;
        TestDescription = value.Description;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            TestCases.Clear();
            var cases = await _runStore.GetTestCasesAsync(_module.Id, CancellationToken.None);
            foreach (var testCase in cases)
            {
                TestCases.Add(new TestCaseEntry(testCase));
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoadSelectedAsync()
    {
        if (SelectedTestCase == null)
        {
            return;
        }

        var version = await _runStore.GetTestCaseVersionAsync(SelectedTestCase.Id, SelectedTestCase.CurrentVersion, CancellationToken.None);
        if (version == null)
        {
            return;
        }

        var settings = JsonSerializer.Deserialize(version.PayloadJson, _module.SettingsType, _jsonOptions);
        if (settings != null)
        {
            _settings.UpdateFrom(settings);
        }
    }

    [RelayCommand]
    private async Task SaveCurrentAsync()
    {
        await EnsureTestCaseAsync(CancellationToken.None);
    }

    public async Task<TestCase> EnsureTestCaseAsync(CancellationToken ct)
    {
        var name = string.IsNullOrWhiteSpace(TestName) ? _module.DisplayName : TestName;
        var payloadJson = JsonSerializer.Serialize(_settings.Settings, _module.SettingsType, _jsonOptions);
        var saved = await _runStore.SaveTestCaseAsync(name, TestDescription, _module.Id, payloadJson, ChangeNote, ct);
        await RefreshAsync();
        SelectedTestCase = TestCases.FirstOrDefault(tc => tc.Id == saved.Id) ?? new TestCaseEntry(saved);
        return saved;
    }

    public sealed class TestCaseEntry
    {
        public TestCaseEntry(TestCase testCase)
        {
            Id = testCase.Id;
            Name = testCase.Name;
            Description = testCase.Description;
            CurrentVersion = testCase.CurrentVersion;
        }

        public Guid Id { get; }
        public string Name { get; }
        public string Description { get; }
        public int CurrentVersion { get; }

        public override string ToString() => $"{Name} (v{CurrentVersion})";
    }
}
