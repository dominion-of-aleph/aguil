using System;
using System.Collections.Generic;
using System.Linq;
using Aguil.Core;
using Aguil.Phlow;
using Avalonia;
using Avalonia.Controls;
using Microsoft.FSharp.Collections;
using Value = Aguil.Core.AlValues.AlValue;

namespace Aguil.Views;

public partial class Inspector : TabControl
{
    public static readonly StyledProperty<IEnumerable<FSharpMap<Value, Value>>?> ViewsProperty =
        AvaloniaProperty.Register<Inspector, IEnumerable<FSharpMap<Value, Value>>?>(nameof(Views));

    public static readonly AttachedProperty<Inspection?> InspectionProperty =
        AvaloniaProperty.RegisterAttached<Control, Inspection?>(nameof(Inspection), typeof(Inspector), inherits: true);

    static Inspector()
    {
        ViewsProperty.Changed.AddClassHandler<Inspector>((inspector, _) =>
            inspector.Show((inspector.Views ?? []).Select(PhlowBuilder.Build)));
    }

    public Inspector()
    {
        InitializeComponent();
    }

    public Inspector(IEnumerable<AlPhlowView> views) : this()
    {
        Show(views);
    }

    protected override Type StyleKeyOverride => typeof(TabControl);

    public Inspection? Inspection
    {
        get => GetValue(InspectionProperty);
        set => SetValue(InspectionProperty, value);
    }

    public IEnumerable<FSharpMap<Value, Value>>? Views
    {
        get => GetValue(ViewsProperty);
        set => SetValue(ViewsProperty, value);
    }

    public static Inspection? GetInspection(Control control)
    {
        return control.GetValue(InspectionProperty);
    }

    public static void SetInspection(Control control, Inspection? inspection)
    {
        control.SetValue(InspectionProperty, inspection);
    }

    private void Show(IEnumerable<AlPhlowView> views)
    {
        ItemsSource = views.OrderBy(view => view.Priority).ToArray();
        SelectedIndex = 0;
    }
}