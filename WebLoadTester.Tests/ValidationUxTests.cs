using System.Threading.Tasks;
using WebLoadTester.Presentation.ViewModels;
using WebLoadTester.Presentation.ViewModels.SettingsViewModels;
using Xunit;

namespace WebLoadTester.Tests;

public class ValidationUxTests
{
    [Fact]
    public void ValidationState_ErrorVisibleOnlyAfterTouched()
    {
        var state = new ValidationState();
        state.SetErrors(new System.Collections.Generic.Dictionary<string, string>
        {
            ["config.name"] = "Имя обязательно"
        });

        Assert.False(state.HasVisibleError("config.name"));

        state.MarkTouched("config.name");

        Assert.True(state.HasVisibleError("config.name"));
    }

    [Fact]
    public void ValidationState_SubmitShowsAllErrors()
    {
        var state = new ValidationState();
        state.SetErrors(new System.Collections.Generic.Dictionary<string, string>
        {
            ["profile.timeoutSeconds"] = "Таймаут должен быть больше 0 секунд."
        });

        Assert.False(state.HasVisibleError("profile.timeoutSeconds"));
        state.ShowAll();
        Assert.True(state.HasVisibleError("profile.timeoutSeconds"));
    }

    [Fact]
    public void ValidationState_FirstVisibleKey_UsesPreferredOrderAfterSubmit()
    {
        var state = new ValidationState();
        state.SetErrors(new System.Collections.Generic.Dictionary<string, string>
        {
            ["profile.timeoutSeconds"] = "timeout",
            ["config.name"] = "name"
        });

        Assert.Null(state.GetFirstVisibleErrorKey(new[] { "config.name", "profile.timeoutSeconds" }));

        state.ShowAll();

        Assert.Equal("config.name", state.GetFirstVisibleErrorKey(new[] { "config.name", "profile.timeoutSeconds" }));
    }

    [Fact]
    public void ValidationState_FirstVisibleKey_ReturnsTableKey_WhenOnlyTableErrorExists()
    {
        var state = new ValidationState();
        state.SetErrors(new System.Collections.Generic.Dictionary<string, string>
        {
            ["table.steps"] = "Добавьте хотя бы один шаг."
        });

        state.ShowAll();

        Assert.Equal("table.steps", state.GetFirstVisibleErrorKey(new[] { "config.name", "table.steps" }));
    }

    [Fact]
    public async Task StartCommand_DisabledWhenProfileOrTableErrorsExist()
    {
        var vm = new MainWindowViewModel();
        await Task.Delay(800);

        var module = vm.UiFamily.SelectedModule;
        Assert.NotNull(module);

        module!.ModuleConfig.UserName = "demo";
        vm.RunProfile.Parallelism = 0;
        await Task.Delay(50);

        Assert.False(vm.StartCommand.CanExecute(null));
        Assert.Contains("Параллелизм", vm.StartValidationMessage);

        vm.RunProfile.Parallelism = 1;
        vm.RunProfile.Iterations = 1;
        vm.RunProfile.TimeoutSeconds = 30;

        var scenarioVm = Assert.IsType<UiScenarioSettingsViewModel>(module.SettingsViewModel);
        scenarioVm.Steps.Clear();
        await Task.Delay(50);

        Assert.False(vm.StartCommand.CanExecute(null));
        Assert.False(string.IsNullOrWhiteSpace(vm.StartValidationMessage));
    }
}
