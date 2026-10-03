using Avalonia;
using Avalonia.Controls;

namespace Aguil.Editor;

public static class Inspect
{
    public static readonly AttachedProperty<object?> TargetProperty =
        AvaloniaProperty.RegisterAttached<Control, object?>("Target", typeof(Inspect), inherits: true);

    public static object? GetTarget(Control control) => control.GetValue(TargetProperty);

    public static void SetTarget(Control control, object? target) => control.SetValue(TargetProperty, target);
}
