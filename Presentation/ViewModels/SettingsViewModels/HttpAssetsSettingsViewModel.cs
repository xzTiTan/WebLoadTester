using WebLoadTester.Modules.HttpAssets;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public sealed class HttpAssetsSettingsViewModel : SettingsViewModelBase
{
    public HttpAssetsSettingsViewModel(HttpAssetsSettings settings)
    {
        Model = settings;
    }

    public HttpAssetsSettings Model { get; }
    public override object Settings => Model;
}
