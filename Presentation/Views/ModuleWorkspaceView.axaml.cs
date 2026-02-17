using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WebLoadTester.Presentation.ViewModels;

namespace WebLoadTester.Presentation.Views;

public partial class ModuleWorkspaceView : UserControl
{
    private MainWindowViewModel? _currentVm;
    private ModuleConfigViewModel? _currentConfig;

    public ModuleWorkspaceView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private MainWindowViewModel? Vm => DataContext as MainWindowViewModel;

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (_currentVm != null)
        {
            _currentVm.PropertyChanged -= OnMainVmPropertyChanged;
            UnsubscribeConfig(_currentVm.SelectedModule?.ModuleConfig);
        }

        _currentVm = Vm;
        if (_currentVm == null)
        {
            return;
        }

        _currentVm.PropertyChanged += OnMainVmPropertyChanged;
        SubscribeConfig(_currentVm.SelectedModule?.ModuleConfig);
    }

    private void OnMainVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_currentVm == null)
        {
            return;
        }

        if (e.PropertyName == nameof(MainWindowViewModel.RequestedScrollNonce))
        {
            ScrollToValidationKey(_currentVm.RequestedScrollToValidationKey);
            return;
        }

        if (e.PropertyName == nameof(MainWindowViewModel.SelectedModule))
        {
            SubscribeConfig(_currentVm.SelectedModule?.ModuleConfig);
        }
    }

    private void SubscribeConfig(ModuleConfigViewModel? config)
    {
        if (_currentConfig != null)
        {
            _currentConfig.PropertyChanged -= OnModuleConfigPropertyChanged;
        }

        _currentConfig = config;
        if (_currentConfig != null)
        {
            _currentConfig.PropertyChanged += OnModuleConfigPropertyChanged;
        }
    }

    private void UnsubscribeConfig(ModuleConfigViewModel? config)
    {
        if (config != null)
        {
            config.PropertyChanged -= OnModuleConfigPropertyChanged;
        }

        _currentConfig = null;
    }

    private void OnModuleConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ModuleConfigViewModel config)
        {
            return;
        }

        if (e.PropertyName == nameof(ModuleConfigViewModel.RequestedScrollNonce))
        {
            ScrollToValidationKey(config.RequestedScrollToValidationKey);
        }
    }

    private void ScrollToValidationKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            var target = ResolveTargetForKey(key);
            if (target == null)
            {
                return;
            }

            target.BringIntoView();
            Dispatcher.UIThread.Post(() =>
            {
                if (target is DataGrid grid)
                {
                    if (grid.SelectedIndex < 0 && grid.ItemsSource is System.Collections.IEnumerable rows && rows.Cast<object>().Any())
                    {
                        grid.SelectedIndex = 0;
                    }

                    grid.Focus();
                    return;
                }

                if (target is SelectingItemsControl list)
                {
                    if (list.SelectedIndex < 0 && list.ItemCount > 0)
                    {
                        list.SelectedIndex = 0;
                    }

                    list.Focus();
                    return;
                }

                target.Focus();
            }, DispatcherPriority.Background);
        }, DispatcherPriority.Background);
    }

    private Control? ResolveTargetForKey(string key)
    {
        return key switch
        {
            ModuleConfigViewModel.ConfigNameKey => this.FindControl<Control>("ConfigNameField"),
            RunProfileViewModel.ParallelismKey => this.FindControl<Control>("ProfileParallelismField"),
            RunProfileViewModel.IterationsKey => this.FindControl<Control>("ProfileIterationsField"),
            RunProfileViewModel.DurationKey => this.FindControl<Control>("ProfileDurationField"),
            RunProfileViewModel.TimeoutKey => this.FindControl<Control>("ProfileTimeoutField"),
            RunProfileViewModel.PauseKey => this.FindControl<Control>("ProfilePauseField"),
            ModuleConfigViewModel.TableStepsKey or ModuleConfigViewModel.TableTargetsKey or ModuleConfigViewModel.TableAssetsKey or ModuleConfigViewModel.TablePortsKey
                => FindFirstCollectionControl(),
            ModuleConfigViewModel.ListEndpointsKey => FindFirstCollectionControl(),
            _ => this.FindControl<Control>("ConfigNameField")
        };
    }

    private Control? FindFirstCollectionControl()
    {
        var host = this.FindControl<Control>("SettingsContentHost");
        if (host == null)
        {
            return null;
        }

        var visual = host.GetVisualDescendants().OfType<Control>();
        var dataGrid = visual.OfType<DataGrid>().FirstOrDefault();
        if (dataGrid != null)
        {
            return dataGrid;
        }

        var listBox = visual.OfType<ListBox>().FirstOrDefault();
        return listBox;
    }

    private void OnConfigNameLostFocus(object? sender, RoutedEventArgs e)
    {
        Vm?.SelectedModule?.ModuleConfig.MarkFieldTouchedCommand.Execute(ModuleConfigViewModel.ConfigNameKey);
    }

    private void OnProfileParallelismLostFocus(object? sender, RoutedEventArgs e)
    {
        Vm?.RunProfile.MarkFieldTouched(RunProfileViewModel.ParallelismKey);
    }

    private void OnProfileIterationsLostFocus(object? sender, RoutedEventArgs e)
    {
        Vm?.RunProfile.MarkFieldTouched(RunProfileViewModel.IterationsKey);
    }

    private void OnProfileDurationLostFocus(object? sender, RoutedEventArgs e)
    {
        Vm?.RunProfile.MarkFieldTouched(RunProfileViewModel.DurationKey);
    }

    private void OnProfileTimeoutLostFocus(object? sender, RoutedEventArgs e)
    {
        Vm?.RunProfile.MarkFieldTouched(RunProfileViewModel.TimeoutKey);
    }

    private void OnProfilePauseLostFocus(object? sender, RoutedEventArgs e)
    {
        Vm?.RunProfile.MarkFieldTouched(RunProfileViewModel.PauseKey);
    }
}
