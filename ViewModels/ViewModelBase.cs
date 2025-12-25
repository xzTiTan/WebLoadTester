using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace WebLoadTester.ViewModels;

public abstract class ViewModelBase : ObservableObject, IDisposable
{
    public virtual void Dispose()
    {
    }
}
