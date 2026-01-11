using WebLoadTester.Modules.HttpPerformance;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public sealed class HttpPerformanceSettingsViewModel : SettingsViewModelBase
{
    public HttpPerformanceSettingsViewModel(HttpPerformanceSettings settings)
    {
        Model = settings;
    }

    public HttpPerformanceSettings Model { get; }
    public override object Settings => Model;
}
