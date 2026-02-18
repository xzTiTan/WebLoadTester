using System;
using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.UiScenario;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels.UiScenario;

public partial class UiStepRowViewModel : ObservableObject
{
    public UiStepRowViewModel(UiStep model)
    {
        Model = model;
        Model.PropertyChanged += (_, _) => RaiseComputed();
    }

    public UiStep Model { get; }

    public UiStepAction Action
    {
        get => Model.Action;
        set
        {
            if (Model.Action == value)
            {
                return;
            }

            Model.Action = value;
            NormalizeForAction();
            RaiseComputed();
        }
    }

    public string Selector
    {
        get => Model.Selector ?? string.Empty;
        set
        {
            if (Model.Selector == value)
            {
                return;
            }

            Model.Selector = value ?? string.Empty;
            RaiseComputed();
        }
    }

    public string Value
    {
        get => Model.Value ?? string.Empty;
        set
        {
            if (Model.Value == value)
            {
                return;
            }

            Model.Value = value ?? string.Empty;
            RaiseComputed();
        }
    }

    public int DelayMs
    {
        get => Model.DelayMs;
        set
        {
            if (Model.DelayMs == value)
            {
                return;
            }

            Model.DelayMs = value;
            RaiseComputed();
        }
    }

    public bool IsSelectorEnabled => Action is UiStepAction.WaitForSelector or UiStepAction.Click or UiStepAction.Fill or UiStepAction.AssertText or UiStepAction.Screenshot;
    public bool IsValueEnabled => Action is UiStepAction.Navigate or UiStepAction.Fill or UiStepAction.AssertText or UiStepAction.Screenshot;
    public bool IsDelayMsEnabled => true;

    public string SelectorWatermark => Action switch
    {
        UiStepAction.WaitForSelector => "CSS/XPath селектор (обяз.)",
        UiStepAction.Click => "CSS/XPath селектор (обяз.)",
        UiStepAction.Fill => "CSS/XPath селектор (обяз.)",
        UiStepAction.AssertText => "CSS/XPath селектор (обяз.)",
        UiStepAction.Screenshot => "Опциональный селектор области",
        _ => "Недоступно для этого действия"
    };

    public string ValueWatermark => Action switch
    {
        UiStepAction.Navigate => "URL (обяз.)",
        UiStepAction.Fill => "Значение для ввода (обяз.)",
        UiStepAction.AssertText => "Ожидаемый текст (обяз.)",
        UiStepAction.Screenshot => "Опциональное имя/суффикс",
        _ => "Недоступно для этого действия"
    };

    public string RowErrorText
    {
        get
        {
            if (Action == UiStepAction.Delay)
            {
                return DelayMs > 0 ? string.Empty : "Для шага «Пауза» DelayMs должен быть > 0.";
            }

            if (Action == UiStepAction.Navigate && string.IsNullOrWhiteSpace(Value))
            {
                return "Для шага «Переход» укажите URL в поле Value.";
            }

            if (Action is UiStepAction.WaitForSelector or UiStepAction.Click or UiStepAction.Fill or UiStepAction.AssertText &&
                string.IsNullOrWhiteSpace(Selector))
            {
                return "Для этого шага обязательно заполнить Selector.";
            }

            if (Action is UiStepAction.Fill or UiStepAction.AssertText && string.IsNullOrWhiteSpace(Value))
            {
                return "Для этого шага обязательно заполнить Value.";
            }

            return string.Empty;
        }
    }

    public bool HasRowError => !string.IsNullOrWhiteSpace(RowErrorText);

    public UiStepRowViewModel Clone()
    {
        return new UiStepRowViewModel(new UiStep
        {
            Action = Action,
            Selector = Selector,
            Value = Value,
            Text = Model.Text,
            DelayMs = DelayMs
        });
    }

    public void Clear()
    {
        Model.Selector = string.Empty;
        Model.Value = string.Empty;
        Model.Text = string.Empty;
        Model.DelayMs = 0;
        RaiseComputed();
    }

    public void NormalizeForAction()
    {
        switch (Action)
        {
            case UiStepAction.Navigate:
                Model.Selector = string.Empty;
                break;
            case UiStepAction.WaitForSelector:
            case UiStepAction.Click:
                Model.Value = string.Empty;
                break;
            case UiStepAction.Delay:
                Model.Selector = string.Empty;
                Model.Value = string.Empty;
                if (Model.DelayMs <= 0)
                {
                    Model.DelayMs = 1000;
                }
                break;
        }

        RaiseComputed();
    }

    private void RaiseComputed()
    {
        OnPropertyChanged(nameof(Action));
        OnPropertyChanged(nameof(Selector));
        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(DelayMs));
        OnPropertyChanged(nameof(IsSelectorEnabled));
        OnPropertyChanged(nameof(IsValueEnabled));
        OnPropertyChanged(nameof(IsDelayMsEnabled));
        OnPropertyChanged(nameof(SelectorWatermark));
        OnPropertyChanged(nameof(ValueWatermark));
        OnPropertyChanged(nameof(RowErrorText));
        OnPropertyChanged(nameof(HasRowError));
    }
}
