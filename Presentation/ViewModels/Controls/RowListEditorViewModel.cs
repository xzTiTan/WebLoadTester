using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WebLoadTester.Presentation.ViewModels.Controls;

public partial class RowListEditorViewModel : ObservableObject
{
    private Func<object?>? _createItem;
    private Action<object?>? _removeItem;
    private Action<object?>? _moveUp;
    private Action<object?>? _moveDown;
    private Action<object?>? _duplicate;
    private Func<IEnumerable<string>>? _validationProvider;
    private Action<object?>? _selectedItemChanged;

    public ObservableCollection<object> Items { get; } = new();

    [ObservableProperty]
    private object? selectedItem;

    [ObservableProperty]
    private int focusRequestToken;

    public IEnumerable<string> ValidationErrors => _validationProvider?.Invoke() ?? [];

    public bool CanRemove => RemoveCommand.CanExecute(null);
    public bool CanMoveUp => MoveUpCommand.CanExecute(null);
    public bool CanMoveDown => MoveDownCommand.CanExecute(null);
    public bool CanDuplicate => DuplicateCommand.CanExecute(null);

    partial void OnSelectedItemChanged(object? value)
    {
        _selectedItemChanged?.Invoke(value);
        RaiseCommandState();
    }

    public void Configure(
        Func<object?> createItem,
        Action<object?> removeItem,
        Action<object?> moveUp,
        Action<object?> moveDown,
        Action<object?> duplicate,
        Func<IEnumerable<string>>? validationProvider,
        Action<object?>? selectedItemChanged = null)
    {
        _createItem = createItem;
        _removeItem = removeItem;
        _moveUp = moveUp;
        _moveDown = moveDown;
        _duplicate = duplicate;
        _validationProvider = validationProvider;
        _selectedItemChanged = selectedItemChanged;

        RaiseCommandState();
    }

    public void SetItems(IEnumerable<object> items)
    {
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }

        if (SelectedItem != null && !Items.Contains(SelectedItem))
        {
            SelectedItem = null;
        }

        RaiseCommandState();
    }

    public void NotifyValidationChanged()
    {
        OnPropertyChanged(nameof(ValidationErrors));
    }

    public void RaiseCommandState()
    {
        RemoveCommand.NotifyCanExecuteChanged();
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
        DuplicateCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanRemove));
        OnPropertyChanged(nameof(CanMoveUp));
        OnPropertyChanged(nameof(CanMoveDown));
        OnPropertyChanged(nameof(CanDuplicate));
    }

    [RelayCommand]
    private void Add()
    {
        var item = _createItem?.Invoke();
        if (item != null)
        {
            SelectedItem = item;
            FocusRequestToken++;
        }

        RaiseCommandState();
    }

    [RelayCommand(CanExecute = nameof(CanMutateSelected))]
    private void Remove() => _removeItem?.Invoke(SelectedItem);

    [RelayCommand(CanExecute = nameof(CanMutateSelected))]
    private void MoveUp() => _moveUp?.Invoke(SelectedItem);

    [RelayCommand(CanExecute = nameof(CanMutateSelected))]
    private void MoveDown() => _moveDown?.Invoke(SelectedItem);

    [RelayCommand(CanExecute = nameof(CanMutateSelected))]
    private void Duplicate()
    {
        var previousSelection = SelectedItem;
        _duplicate?.Invoke(SelectedItem);

        if (SelectedItem != null && !ReferenceEquals(previousSelection, SelectedItem))
        {
            FocusRequestToken++;
        }
    }

    private bool CanMutateSelected() => SelectedItem != null;
}
