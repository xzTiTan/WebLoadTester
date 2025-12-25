using CommunityToolkit.Mvvm.ComponentModel;

namespace WebLoadTester.ViewModels;

public abstract class ViewModelBase : ObservableObject, IDisposable
{
    public virtual void Dispose()
    {
    }
}
