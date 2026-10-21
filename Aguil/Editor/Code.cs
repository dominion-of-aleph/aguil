using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;

namespace Aguil.Editor;

// Attached properties for AvaloniaEdit's TextEditor.
public static class Code
{
    // A workaround to bind to TextEditor.Text.
    public static readonly AttachedProperty<string?> TextProperty =
        AvaloniaProperty.RegisterAttached<TextEditor, string?>("Text", typeof(Code),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly AttachedProperty<bool> FollowCaretProperty =
        AvaloniaProperty.RegisterAttached<TextEditor, bool>("FollowCaret", typeof(Code));

    private static readonly AttachedProperty<EventHandler?> CaretHandlerProperty =
        AvaloniaProperty.RegisterAttached<TextEditor, EventHandler?>("CaretHandler", typeof(Code));

    // a grammar name e.g. elixir
    public static readonly AttachedProperty<string?> GrammarProperty =
        AvaloniaProperty.RegisterAttached<TextEditor, string?>("Grammar", typeof(Code));

    // keys to action names, see Keymaps
    public static readonly AttachedProperty<IReadOnlyDictionary<string, string>?> KeymapProperty =
        AvaloniaProperty.RegisterAttached<TextEditor, IReadOnlyDictionary<string, string>?>("Keymap", typeof(Code));

    // removes the installed keymap
    private static readonly AttachedProperty<IDisposable?> KeymapHandlerProperty =
        AvaloniaProperty.RegisterAttached<TextEditor, IDisposable?>("KeymapHandler", typeof(Code));

    // only exists while the editor is loaded; rebuilt when its document changes
    private static readonly AttachedProperty<SnippetColorizer?> ColorizerProperty =
        AvaloniaProperty.RegisterAttached<TextEditor, SnippetColorizer?>("Colorizer", typeof(Code));

    static Code()
    {
        FollowCaretProperty.Changed.AddClassHandler<TextEditor>((editor, _) =>
        {
            if (editor.GetValue(CaretHandlerProperty) is { } previous)
                editor.TextArea.Caret.PositionChanged -= previous;
            EventHandler? handler = GetFollowCaret(editor)
                ? (_, _) =>
                {
                    var view = editor.TextArea.TextView;
                    var point = view.GetVisualPosition(editor.TextArea.Caret.Position, VisualYPosition.LineTop);
                    if (view.TranslatePoint(point - view.ScrollOffset, editor) is { } at)
                        editor.BringIntoView(new Rect(at, new Size(1, view.DefaultLineHeight)));
                }
                : null;
            editor.SetValue(CaretHandlerProperty, handler);
            if (handler is not null) editor.TextArea.Caret.PositionChanged += handler;
        });
        TextProperty.Changed.AddClassHandler<TextEditor>((editor, _) =>
        {
            if (editor.Text != (GetText(editor) ?? "")) editor.Text = GetText(editor) ?? "";
        });
        GrammarProperty.Changed.AddClassHandler<TextEditor>((editor, _) => Rebuild(editor));
        KeymapProperty.Changed.AddClassHandler<TextEditor>((editor, _) =>
        {
            editor.GetValue(KeymapHandlerProperty)?.Dispose();
            editor.SetValue(KeymapHandlerProperty,
                GetKeymap(editor) is { } keymap ? Keymaps.install(editor.TextArea, keymap) : null);
        });
        TextEditor.DocumentProperty.Changed.AddClassHandler<TextEditor>((editor, _) => Rebuild(editor));
        Control.LoadedEvent.AddClassHandler<TextEditor>((editor, _) =>
        {
            editor.TextChanged += TextChanged;
            Rebuild(editor);
        });
        Control.UnloadedEvent.AddClassHandler<TextEditor>((editor, _) =>
        {
            editor.TextChanged -= TextChanged;
            Rebuild(editor);
        });
        // the colorizer picks light or dark colors when it draws
        ThemeVariantScope.ActualThemeVariantProperty.Changed.AddClassHandler<TextEditor>((editor, _) =>
        {
            if (editor.GetValue(ColorizerProperty) is not null)
                editor.TextArea.TextView.Redraw();
        });
    }

    public static bool GetFollowCaret(TextEditor editor)
    {
        return editor.GetValue(FollowCaretProperty);
    }

    public static void SetFollowCaret(TextEditor editor, bool value)
    {
        editor.SetValue(FollowCaretProperty, value);
    }

    private static void TextChanged(object? sender, EventArgs args)
    {
        var editor = (TextEditor)sender!;
        if (GetText(editor) != editor.Text) editor.SetCurrentValue(TextProperty, editor.Text);
    }

    public static string? GetText(TextEditor editor)
    {
        return editor.GetValue(TextProperty);
    }

    public static void SetText(TextEditor editor, string? value)
    {
        editor.SetValue(TextProperty, value);
    }

    public static string? GetGrammar(TextEditor editor)
    {
        return editor.GetValue(GrammarProperty);
    }

    public static IReadOnlyDictionary<string, string>? GetKeymap(TextEditor editor)
    {
        return editor.GetValue(KeymapProperty);
    }

    public static void SetKeymap(TextEditor editor, IReadOnlyDictionary<string, string>? value)
    {
        editor.SetValue(KeymapProperty, value);
    }

    public static void SetGrammar(TextEditor editor, string? value)
    {
        editor.SetValue(GrammarProperty, value);
    }

    private static void Rebuild(TextEditor editor)
    {
        // Chuck the old Colorizer, we set and toss it
        editor.GetValue(ColorizerProperty)?.Dispose();
        var grammar = GetGrammar(editor);
        editor.SetValue(ColorizerProperty,
            !string.IsNullOrEmpty(grammar) && editor is { IsLoaded: true, Document: not null }
                ? new SnippetColorizer(editor.TextArea.TextView, grammar)
                : null);
    }
}