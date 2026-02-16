using System;
using System.Globalization;
using Avalonia.Data.Converters;
using WebLoadTester.Modules.UiScenario;

namespace WebLoadTester.Presentation.Converters;

public sealed class UiScenarioActionDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not UiStepAction action)
        {
            return string.Empty;
        }

        return action switch
        {
            UiStepAction.Navigate => "Переход",
            UiStepAction.WaitForSelector => "Ожидание элемента",
            UiStepAction.Click => "Клик",
            UiStepAction.Fill => "Ввод текста",
            UiStepAction.AssertText => "Проверка текста",
            UiStepAction.Screenshot => "Скриншот",
            UiStepAction.Delay => "Пауза",
            _ => action.ToString()
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
}
