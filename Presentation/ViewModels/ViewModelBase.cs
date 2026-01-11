using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WebLoadTester.Presentation.ViewModels;

/// <summary>
/// Базовый класс ViewModel с поддержкой IDisposable.
/// </summary>
public abstract class ViewModelBase : ObservableObject, IDisposable
{
    /// <summary>
    /// Освобождает ресурсы ViewModel, если они есть.
    /// </summary>
    public virtual void Dispose()
    {
    }
}
