using System.Linq;
using WebLoadTester.Modules.HttpAssets;
using WebLoadTester.Modules.UiScenario;
using WebLoadTester.Modules.UiSnapshot;
using WebLoadTester.Presentation.ViewModels.SettingsViewModels;
using Xunit;

namespace WebLoadTester.Tests;

public class TableToolbarCommandsTests
{
    [Fact]
    public void UiScenario_DuplicateAndMoveCommands_UpdateOrderAndSelection()
    {
        var settings = new UiScenarioSettings
        {
            Steps =
            {
                new UiStep { Value = "first" },
                new UiStep { Value = "second" },
                new UiStep { Value = "third" }
            }
        };
        var vm = new UiScenarioSettingsViewModel(settings)
        {
            SelectedStep = settings.Steps[1]
        };

        vm.DuplicateSelectedStepCommand.Execute(null);

        Assert.Equal(4, vm.Steps.Count);
        Assert.Equal("second", vm.Steps[2].Value);
        Assert.Same(vm.Steps[2], vm.SelectedStep);

        vm.MoveSelectedStepUpCommand.Execute(null);
        Assert.Equal(new[] { "first", "second", "second", "third" }, vm.Steps.Select(s => s.Value));
        Assert.Same(vm.Steps[1], vm.SelectedStep);

        vm.MoveSelectedStepDownCommand.Execute(null);
        Assert.Equal(new[] { "first", "second", "second", "third" }, vm.Steps.Select(s => s.Value));
        Assert.Same(vm.Steps[2], vm.SelectedStep);
    }

    [Fact]
    public void UiSnapshot_AddDeleteDuplicateCommands_UpdateCollectionAndSelection()
    {
        var settings = new UiSnapshotSettings();
        var vm = new UiSnapshotSettingsViewModel(settings);

        vm.AddTargetCommand.Execute(null);
        Assert.Single(vm.Targets);
        Assert.Same(vm.Targets[0], vm.SelectedTarget);

        vm.DuplicateSelectedTargetCommand.Execute(null);
        Assert.Equal(2, vm.Targets.Count);
        Assert.Same(vm.Targets[1], vm.SelectedTarget);

        vm.RemoveSelectedTargetCommand.Execute(null);
        Assert.Single(vm.Targets);
    }

    [Fact]
    public void HttpAssets_DuplicateInsertsAfterSelected_AndSelectsCopy()
    {
        var settings = new HttpAssetsSettings
        {
            Assets =
            {
                new AssetItem { Name = "A", Url = "https://a" },
                new AssetItem { Name = "B", Url = "https://b" }
            }
        };

        var vm = new HttpAssetsSettingsViewModel(settings)
        {
            SelectedAsset = settings.Assets[0]
        };

        vm.DuplicateSelectedAssetCommand.Execute(null);

        Assert.Equal(3, vm.Assets.Count);
        Assert.Equal(new[] { "A", "A Copy", "B" }, vm.Assets.Select(a => a.Name));
        Assert.Same(vm.Assets[1], vm.SelectedAsset);
    }
}
