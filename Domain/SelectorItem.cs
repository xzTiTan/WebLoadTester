using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WebLoadTester.Domain;

public sealed partial class SelectorItem : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();

    [ObservableProperty]
    private string text = string.Empty;

    public override string ToString() => Text;
}
