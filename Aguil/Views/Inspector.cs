using System;
using System.Collections.Generic;
using System.Linq;
using Aguil.Phlow;
using Avalonia;
using Avalonia.Controls;
using Microsoft.FSharp.Collections;
using Value = Aguil.Core.AlValues.AlValue;

namespace Aguil.Views;

public class Inspector : TabControl
{
    protected override Type StyleKeyOverride => typeof(TabControl);

    public static readonly StyledProperty<IEnumerable<FSharpMap<Value, Value>>?> ViewsProperty =
        AvaloniaProperty.Register<Inspector, IEnumerable<FSharpMap<Value, Value>>?>(nameof(Views));

    static Inspector() => ViewsProperty.Changed.AddClassHandler<Inspector>((inspector, _) =>
        inspector.Show((inspector.Views ?? []).Select(PhlowBuilder.Build)));

    public Inspector() => Padding = new Thickness(0);

    public Inspector(IEnumerable<AlPhlowView> views) : this() => Show(views);

    public IEnumerable<FSharpMap<Value, Value>>? Views
    {
        get => GetValue(ViewsProperty);
        set => SetValue(ViewsProperty, value);
    }

    private void Show(IEnumerable<AlPhlowView> views)
    {
        ItemsSource = views.OrderBy(view => view.Priority)
            .Select(view => new TabItem { Header = view.Title, Content = view.Content }).ToArray();
        SelectedIndex = 0;
    }
}
