using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using WebLoadTester.Presentation.ViewModels;

namespace WebLoadTester;

/// <summary>
/// Given a view model, returns the corresponding view if possible.
/// </summary>
[RequiresUnreferencedCode(
    "Default implementation of ViewLocator involves reflection which may be trimmed away.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
/// <summary>
/// Сопоставляет ViewModel с соответствующим View по имени типа.
/// </summary>
public class ViewLocator : IDataTemplate
{
    /// <summary>
    /// Создаёт экземпляр View для переданной ViewModel.
    /// </summary>
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return new TextBlock { Text = "Not Found: " + name };
    }

    /// <summary>
    /// Проверяет, подходит ли объект для данного шаблона.
    /// </summary>
    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
