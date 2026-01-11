using WebLoadTester.Modules.HttpFunctional;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public sealed class HttpFunctionalSettingsViewModel : SettingsViewModelBase
{
    public HttpFunctionalSettingsViewModel(HttpFunctionalSettings settings)
    {
        Model = settings;
    }

    public HttpFunctionalSettings Model { get; }
    public override object Settings => Model;
}
