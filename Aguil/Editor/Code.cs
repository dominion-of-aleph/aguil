using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using AvaloniaEdit;

namespace Aguil.Editor;

// Attached properties for AvaloniaEdit's TextEditor.
public static class Code
{
    // A workaround to bind to TextEditor.Text.
    public static readonly AttachedProperty<string?> TextProperty =
        AvaloniaProperty.RegisterAttached<TextEditor, string?>("Text", typeof(Code));

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
        TextProperty.Changed.AddClassHandler<TextEditor>((editor, _) => editor.Text = GetText(editor) ?? "");
        GrammarProperty.Changed.AddClassHandler<TextEditor>((editor, _) => Rebuild(editor));
        KeymapProperty.Changed.AddClassHandler<TextEditor>((editor, _) =>
        {
            editor.GetValue(KeymapHandlerProperty)?.Dispose();
            editor.SetValue(KeymapHandlerProperty,
                GetKeymap(editor) is { } keymap ? Keymaps.install(editor.TextArea, keymap) : null);
        });
        TextEditor.DocumentProperty.Changed.AddClassHandler<TextEditor>((editor, _) => Rebuild(editor));
        Control.LoadedEvent.AddClassHandler<TextEditor>((editor, _) => Rebuild(editor));
        Control.UnloadedEvent.AddClassHandler<TextEditor>((editor, _) => Rebuild(editor));
        // the colorizer picks light or dark colors when it draws
        ThemeVariantScope.ActualThemeVariantProperty.Changed.AddClassHandler<TextEditor>((editor, _) =>
        {
            if (editor.GetValue(ColorizerProperty) is not null)
                editor.TextArea.TextView.Redraw();
        });
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
        editor.SetValue(ColorizerProperty, grammar is not null && editor is { IsLoaded: true, Document: not null }
            ? new SnippetColorizer(editor.TextArea.TextView, grammar)
            : null);
    }
}