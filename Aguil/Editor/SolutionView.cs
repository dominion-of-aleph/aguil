using System;
using System.Linq;
using Aguil.Core;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using AvaloniaEdit;

namespace Aguil.Editor;

public static class SolutionView
{
    public static readonly AttachedProperty<AlEvaluation.Solution?> SolutionProperty =
        AvaloniaProperty.RegisterAttached<TabControl, AlEvaluation.Solution?>("Solution", typeof(SolutionView));

    static SolutionView() => SolutionProperty.Changed.AddClassHandler<TabControl>((tabs, _) =>
        Render(tabs, GetSolution(tabs)));

    public static AlEvaluation.Solution? GetSolution(TabControl tabs) => tabs.GetValue(SolutionProperty);

    public static void SetSolution(TabControl tabs, AlEvaluation.Solution? solution) =>
        tabs.SetValue(SolutionProperty, solution);

    private static void Render(TabControl tabs, AlEvaluation.Solution? solution)
    {
        tabs.ItemsSource = solution is null ? [] : Tabs(solution);
        tabs.SelectedIndex = 0;
    }

    public static TabItem[] Tabs(AlEvaluation.Solution solution) =>
    [
        new TabItem
        {
            Header = "Raw",
            Content = Preview(new StackPanel { [ResultView.ResultProperty] = solution.Result })
        },
        .. solution.Result.Bindings.Values.Select(binding => BindingTab(binding, solution.Views[binding.Symbol]))
    ];

    public static TabItem BindingTab(AlEvaluation.Binding<AlValues.AlValue> binding,
        AlEvaluation.EvaluationContext result) => new()
        {
            Header = binding.Symbol,
            DataContext = binding,
            Content = new TabControl
            {
                Classes = { "view-tabs" },
                Padding = new Thickness(0),
                ItemsSource = AlViews.forBinding(binding, result).Select(description => new TabItem
                {
                    Header = PhlowView.Text(description, "title"),
                    Content = Preview(PhlowView.Render(description))
                }).ToArray(),
                SelectedIndex = 0
            }
        };

    public static Control Preview(Control content)
    {
        Control viewport;
        if (content is TextEditor editor)
        {
            editor.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            editor.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            viewport = editor;
        }
        else viewport = new ScrollViewer
        {
            Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        viewport.MaxHeight = 240;
        var grip = new Thumb
        {
            Classes = { "preview-resize" },
            Cursor = new Cursor(StandardCursorType.SizeNorthSouth),
            [Grid.RowProperty] = 1,
            [ToolTip.TipProperty] = "Drag to resize preview"
        };
        grip.DragDelta += (_, e) =>
        {
            var height = Math.Clamp(viewport.Bounds.Height + e.Vector.Y, 48, 600);
            viewport.MaxHeight = height;
            viewport.Height = height;
        };
        return new Grid { RowDefinitions = new RowDefinitions("Auto,Auto"), Children = { viewport, grip } };
    }
}
