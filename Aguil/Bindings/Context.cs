using Aguil.Views;
using Avalonia;
using Avalonia.Controls;

namespace Aguil.Bindings;

public sealed class Context : AvaloniaObject
{
    public static readonly AttachedProperty<string?> BranchProperty =
        AvaloniaProperty.RegisterAttached<Context, Control, string?>("Branch", inherits: true);

    public static string? GetBranch(Control control)
    {
        return control.GetValue(BranchProperty);
    }

    public static void SetBranch(Control control, string? value)
    {
        control.SetValue(BranchProperty, value);
    }

    public static string? BranchFor(Control control)
    {
        return Inspector.GetInspection(control)?.Branch ?? GetBranch(control);
    }
}