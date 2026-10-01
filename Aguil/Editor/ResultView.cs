using Aguil.Core;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using AvaloniaEdit;

namespace Aguil.Editor;

public static class ResultView
{
    public static readonly AttachedProperty<AlEvaluation.EvaluationContext?> ResultProperty =
        AvaloniaProperty.RegisterAttached<StackPanel, AlEvaluation.EvaluationContext?>("Result", typeof(ResultView));

    static ResultView() => ResultProperty.Changed.AddClassHandler<StackPanel>((panel, _) =>
        Render(panel, GetResult(panel)));

    public static AlEvaluation.EvaluationContext? GetResult(StackPanel panel) => panel.GetValue(ResultProperty);

    public static void SetResult(StackPanel panel, AlEvaluation.EvaluationContext? result) =>
        panel.SetValue(ResultProperty, result);

    private static void Render(StackPanel panel, AlEvaluation.EvaluationContext? result)
    {
        panel.Children.Clear();
        if (result is null)
            return;

        void Box(object value, string text) => panel.Children.Add(new Border
        {
            Classes = { "binding" },
            DataContext = value,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Child = new TextEditor { Text = text, IsReadOnly = true }
        });

        void Section(string separator, AlEvaluation.Decoded section)
        {
            foreach (var binding in section.Values)
                Box(binding, AlEvaluation.Binding.pretty(separator, 80, binding));
            foreach (var failure in section.Failures)
                Box(failure, AlEvaluation.Binding.prettyFailure(separator, failure));
        }

        Section(" = ", result.Bindings);
        Section(" : ", result.Constraints);
        Section(" => ", result.Store);
        Box(result, result.Context);

        var copy = new Button
        {
            Content = "Copy all",
            Focusable = false,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left
        };
        copy.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(panel)?.Clipboard is { } clipboard)
                await clipboard.SetTextAsync(result.Pretty(80));
        };
        panel.Children.Add(copy);
    }
}