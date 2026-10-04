using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Aguil.Elements;

public class Stencil<T, TControl>(Func<TControl> create, Action<TControl, T> bind) : IRecyclingDataTemplate
    where TControl : Control
{
    public bool Match(object? data) => data is T;

    public Control Build(object? data) => Build(data, null);

    public Control Build(object? data, Control? existing)
    {
        var control = (TControl?)existing ?? create();
        bind(control, (T)data!);
        return control;
    }
}
