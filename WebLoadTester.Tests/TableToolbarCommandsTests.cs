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

        var vm = new UiScenarioSettingsViewModel(settings);
        vm.SelectedStepRow = vm.StepRows[1];

        vm.StepsEditor.DuplicateCommand.Execute(null);

        Assert.Equal(4, vm.StepRows.Count);
        Assert.Equal("second", vm.StepRows[2].Value);
        Assert.Same(vm.StepRows[2], vm.SelectedStepRow);

        vm.StepsEditor.MoveUpCommand.Execute(null);
        Assert.Equal(new[] { "first", "second", "second", "third" }, vm.StepRows.Select(s => s.Value));
        Assert.Same(vm.StepRows[1], vm.SelectedStepRow);

        vm.StepsEditor.MoveDownCommand.Execute(null);
        Assert.Equal(new[] { "first", "second", "second", "third" }, vm.StepRows.Select(s => s.Value));
        Assert.Same(vm.StepRows[2], vm.SelectedStepRow);
    }

    [Fact]
    public void UiSnapshot_AddDeleteDuplicateCommands_UpdateCollectionAndSelection()
    {
        var settings = new UiSnapshotSettings();
        var vm = new UiSnapshotSettingsViewModel(settings);

        vm.TargetRows.Clear();
        vm.TargetsEditor.SetItems(vm.TargetRows.Cast<object>());
        vm.SelectedTargetRow = null;

        vm.TargetsEditor.AddCommand.Execute(null);
        Assert.Single(vm.TargetRows);
        Assert.Same(vm.TargetRows[0], vm.SelectedTargetRow);

        vm.TargetsEditor.DuplicateCommand.Execute(null);
        Assert.Equal(2, vm.TargetRows.Count);
        Assert.Same(vm.TargetRows[1], vm.SelectedTargetRow);

        vm.TargetsEditor.RemoveCommand.Execute(null);
        Assert.Single(vm.TargetRows);
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

        var vm = new HttpAssetsSettingsViewModel(settings);
        vm.SelectedAssetRow = vm.AssetRows[0];

        vm.AssetsEditor.DuplicateCommand.Execute(null);

        Assert.Equal(3, vm.AssetRows.Count);
        Assert.Equal(new[] { "A", "A", "B" }, vm.AssetRows.Select(a => a.Name));
        Assert.Same(vm.AssetRows[1], vm.SelectedAssetRow);
    }
}
