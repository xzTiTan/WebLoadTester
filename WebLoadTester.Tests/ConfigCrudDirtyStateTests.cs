using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Storage;
using WebLoadTester.Presentation.ViewModels;
using WebLoadTester.Presentation.ViewModels.SettingsViewModels;
using Xunit;

namespace WebLoadTester.Tests;

public class ConfigCrudDirtyStateTests
{
    [Fact]
    public async Task Save_CreatesNewVersion_ForExistingConfig()
    {
        var repo = new FakeTestCaseRepository();
        var service = new ModuleConfigService(repo);
        var run = new RunParametersDto();

        await service.SaveNewAsync("Demo", "http.functional", "first", new DummySettings(), run, CancellationToken.None);
        await service.SaveNewAsync("Demo", "http.functional", "second", new DummySettings(), run, CancellationToken.None);

        var testCase = await repo.GetByNameAsync("Demo_HTTPФункциональные", CancellationToken.None);
        Assert.NotNull(testCase);
        Assert.Equal(2, testCase!.CurrentVersion);
    }

    [Fact]
    public async Task DirtyState_IsSetOnChanges_AndResetAfterSave()
    {
        var vm = CreateVm();
        vm.Config.UserName = "Demo";
        vm.Config.Description = "desc";
        vm.TestModuleSettingsViewModel.Value = "changed";
        Assert.True(vm.Config.IsDirty);

        await vm.Config.SaveCommand.ExecuteAsync(null);
        Assert.False(vm.Config.IsDirty);

        vm.RunProfile.TimeoutSeconds = 99;
        Assert.True(vm.Config.IsDirty);
    }

    [Fact]
    public void Guard_Cancel_DoesNotExecutePendingAction()
    {
        var vm = CreateVm();
        vm.Config.UserName = "Demo";
        vm.TestModuleSettingsViewModel.Value = "dirty";

        var executed = false;
        var allowed = vm.Config.TryRequestLeave(() =>
        {
            executed = true;
            return Task.CompletedTask;
        });

        Assert.False(allowed);
        Assert.True(vm.Config.IsDirtyPromptVisible);

        vm.Config.CancelGuardActionCommand.Execute(null);

        Assert.False(executed);
        Assert.False(vm.Config.IsDirtyPromptVisible);
    }

    private static TestHarness CreateVm()
    {
        var runStore = new FakeRunStore();
        var runProfile = new RunProfileViewModel(runStore);
        var settingsVm = new DummySettingsViewModel();
        var module = new DummyModule();
        var repo = new FakeTestCaseRepository();
        var service = new ModuleConfigService(repo);
        var vm = new ModuleConfigViewModel(service, repo, module, settingsVm, runProfile);
        return new TestHarness(vm, runProfile, settingsVm);
    }

    private sealed record TestHarness(ModuleConfigViewModel Config, RunProfileViewModel RunProfile, DummySettingsViewModel TestModuleSettingsViewModel);

    private sealed class DummySettingsViewModel : SettingsViewModelBase
    {
        private string _value = string.Empty;

        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        public override object Settings => new DummySettings { Value = Value };
        public override string Title => "Dummy";

        public override void UpdateFrom(object settings)
        {
            if (settings is DummySettings s)
            {
                Value = s.Value;
            }
        }
    }

    private sealed class DummyModule : ITestModule
    {
        public string Id => "http.functional";
        public string DisplayName => "Dummy";
        public string Description => "Dummy";
        public TestFamily Family => TestFamily.HttpTesting;
        public Type SettingsType => typeof(DummySettings);
        public object CreateDefaultSettings() => new DummySettings();
        public IReadOnlyList<string> Validate(object settings) => Array.Empty<string>();
        public Task<ModuleResult> ExecuteAsync(object settings, IRunContext ctx, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class DummySettings
    {
        public string Value { get; set; } = string.Empty;
    }

    private sealed class FakeTestCaseRepository : ITestCaseRepository
    {
        private readonly Dictionary<string, TestCase> _casesByName = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<Guid, List<TestCaseVersion>> _versions = new();

        public Task<IReadOnlyList<TestCase>> ListAsync(string moduleType, CancellationToken ct)
        {
            IReadOnlyList<TestCase> result = new List<TestCase>(_casesByName.Values);
            return Task.FromResult(result);
        }

        public Task<TestCase?> GetAsync(Guid testCaseId, CancellationToken ct)
        {
            foreach (var c in _casesByName.Values)
            {
                if (c.Id == testCaseId)
                {
                    return Task.FromResult<TestCase?>(c);
                }
            }

            return Task.FromResult<TestCase?>(null);
        }

        public Task<TestCase?> GetByNameAsync(string name, CancellationToken ct)
        {
            _casesByName.TryGetValue(name, out var testCase);
            return Task.FromResult<TestCase?>(testCase);
        }

        public Task<TestCaseVersion?> GetVersionAsync(Guid testCaseId, int version, CancellationToken ct)
        {
            if (_versions.TryGetValue(testCaseId, out var list))
            {
                var item = list.Find(v => v.VersionNumber == version);
                return Task.FromResult<TestCaseVersion?>(item);
            }

            return Task.FromResult<TestCaseVersion?>(null);
        }

        public Task<TestCase> SaveVersionAsync(string name, string description, string moduleType, string payloadJson, string changeNote, CancellationToken ct)
        {
            if (!_casesByName.TryGetValue(name, out var testCase))
            {
                testCase = new TestCase
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Description = description,
                    ModuleType = moduleType,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    CurrentVersion = 0
                };
                _casesByName[name] = testCase;
                _versions[testCase.Id] = new List<TestCaseVersion>();
            }

            testCase.CurrentVersion++;
            testCase.Description = description;
            testCase.UpdatedAt = DateTimeOffset.UtcNow;
            _versions[testCase.Id].Add(new TestCaseVersion
            {
                Id = Guid.NewGuid(),
                TestCaseId = testCase.Id,
                VersionNumber = testCase.CurrentVersion,
                ChangedAt = DateTimeOffset.UtcNow,
                ChangeNote = changeNote,
                PayloadJson = payloadJson
            });

            return Task.FromResult(testCase);
        }

        public Task SetCurrentVersionAsync(Guid testCaseId, int version, CancellationToken ct) => Task.CompletedTask;
        public Task DeleteAsync(Guid testCaseId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeRunStore : IRunStore
    {
        public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<TestCase>> GetTestCasesAsync(string moduleType, CancellationToken ct) => Task.FromResult<IReadOnlyList<TestCase>>(Array.Empty<TestCase>());
        public Task<TestCaseVersion?> GetTestCaseVersionAsync(Guid testCaseId, int version, CancellationToken ct) => Task.FromResult<TestCaseVersion?>(null);
        public Task<TestCase> SaveTestCaseAsync(string name, string description, string moduleType, string payloadJson, string changeNote, CancellationToken ct) => throw new NotImplementedException();
        public Task DeleteTestCaseAsync(Guid testCaseId, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<RunProfile>> GetRunProfilesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<RunProfile>>(Array.Empty<RunProfile>());
        public Task<RunProfile> SaveRunProfileAsync(RunProfile profile, CancellationToken ct) => Task.FromResult(profile);
        public Task DeleteRunProfileAsync(Guid profileId, CancellationToken ct) => Task.CompletedTask;
        public Task CreateRunAsync(TestRun run, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateRunAsync(TestRun run, CancellationToken ct) => Task.CompletedTask;
        public Task AddRunItemsAsync(IEnumerable<RunItem> items, CancellationToken ct) => Task.CompletedTask;
        public Task AddArtifactsAsync(IEnumerable<ArtifactRecord> artifacts, CancellationToken ct) => Task.CompletedTask;
        public Task AddTelegramNotificationAsync(TelegramNotification notification, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<TestRunSummary>> QueryRunsAsync(RunQuery query, CancellationToken ct) => Task.FromResult<IReadOnlyList<TestRunSummary>>(Array.Empty<TestRunSummary>());
        public Task<TestRunDetail?> GetRunDetailAsync(string runId, CancellationToken ct) => Task.FromResult<TestRunDetail?>(null);
        public Task DeleteRunAsync(string runId, CancellationToken ct) => Task.CompletedTask;
    }
}
