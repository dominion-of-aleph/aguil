using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;

namespace Aguil.Editor;

public static class ReadOnlyText
{
    public static TextEditor CreateCell(string? grammar = null)
    {
        var editor = Create("", grammar);
        editor.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        editor.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        editor.TextArea.Caret.PositionChanged += (_, _) =>
        {
            var view = editor.TextArea.TextView;
            var point = view.GetVisualPosition(editor.TextArea.Caret.Position, VisualYPosition.LineTop);
            if (view.TranslatePoint(point - view.ScrollOffset, editor) is { } at)
                editor.BringIntoView(new Rect(at, new Size(1, view.DefaultLineHeight)));
        };
        return editor;
    }

    public static TextEditor Create(string text, string? grammar = null)
    {
        var editor = new TextEditor
        {
            Text = text,
            IsReadOnly = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        editor.Options.AllowScrollBelowDocument = false;
        Code.SetKeymap(editor, Keymaps.defaults);
        Code.SetGrammar(editor, grammar);
        return editor;
    }
}