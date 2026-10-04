using Avalonia;
using Avalonia.Controls;

namespace Aguil.Phlow;

public static class InspectionTarget
{
    public static readonly AttachedProperty<object?> TargetProperty =
        AvaloniaProperty.RegisterAttached<Control, object?>("Target", typeof(InspectionTarget), inherits: true);

    public static object? GetTarget(Control control) => control.GetValue(TargetProperty);

    public static void SetTarget(Control control, object? target) => control.SetValue(TargetProperty, target);
}