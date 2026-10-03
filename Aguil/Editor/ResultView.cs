using Aguil.Core;
using Avalonia;
using Avalonia.Controls;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;

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

        void Box(object value, string text, object? target = null)
        {
            var editor = new TextEditor { Text = text, IsReadOnly = true };
            editor.TextArea.Caret.PositionChanged += (_, _) =>
            {
                var view = editor.TextArea.TextView;
                var point = view.GetVisualPosition(editor.TextArea.Caret.Position, VisualYPosition.LineTop);
                if (view.TranslatePoint(point - view.ScrollOffset, editor) is { } at)
                    editor.BringIntoView(new Rect(at, new Size(1, view.DefaultLineHeight)));
            };
            panel.Children.Add(new Border
            {
                Classes = { "binding" },
                DataContext = value,
                [Inspect.TargetProperty] = target,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Child = editor
            });
        }

        void Section(string separator, AlEvaluation.Decoded section)
        {
            foreach (var binding in section.Values)
                Box(binding, AlEvaluation.Binding.pretty(separator, 80, binding), binding.Value);
            foreach (var failure in section.Failures)
                Box(failure, AlEvaluation.Binding.prettyFailure(separator, failure));
        }

        Section(" = ", result.Bindings);
        Section(" : ", result.Constraints);
        Section(" => ", result.Store);
    }
}
