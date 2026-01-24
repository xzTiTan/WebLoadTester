using CommunityToolkit.Mvvm.ComponentModel;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

/// <summary>
/// Базовый класс ViewModel настроек модулей.
/// </summary>
public abstract partial class SettingsViewModelBase : ObservableObject
{
    /// <summary>
    /// Возвращает объект настроек, связанный с ViewModel.
    /// </summary>
    public abstract object Settings { get; }
    /// <summary>
    /// Заголовок секции настроек для UI.
    /// </summary>
    public abstract string Title { get; }
    /// <summary>
    /// Обновляет значения ViewModel из внешнего объекта настроек.
    /// </summary>
    public abstract void UpdateFrom(object settings);
}
