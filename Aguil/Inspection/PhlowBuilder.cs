using System;
using Aguil.Editor;
using Avalonia.Controls;
using AvaloniaEdit;
using Microsoft.FSharp.Collections;
using Value = Aguil.Core.AlValues.AlValue;

namespace Aguil.Inspection;

public static class PhlowBuilder
{
    public static Control Render(FSharpMap<Value, Value> fields)
    {
        Control control = Text(fields, "view") switch
        {
            "text" => RenderText(Text(fields, "text"),
                fields.TryGetValue(Value.NewAlAtom("grammar"), out var grammar) && grammar is Value.AlText text
                    ? text.Item : null),
            var kind => throw new NotSupportedException($"Unknown view: {kind}")
        };
        if (fields.TryGetValue(Value.NewAlAtom("target"), out var target))
            InspectionTarget.SetTarget(control, target);
        if (fields.TryGetValue(Value.NewAlAtom("height"), out var height))
            control.Height = height switch
            {
                Value.AlInteger integer => (double)integer.Item,
                Value.AlFloat number => number.Item,
                _ => double.NaN
            };
        return control;
    }

    public static TextEditor RenderText(string text, string? grammar = null)
    {
        var editor = new TextEditor { Text = text, IsReadOnly = true };
        editor.Options.AllowScrollBelowDocument = false;
        Code.SetKeymap(editor, Keymaps.defaults);
        Code.SetGrammar(editor, grammar);
        return editor;
    }

    public static string Text(FSharpMap<Value, Value> fields, string key) =>
        fields.TryGetValue(Value.NewAlAtom(key), out var value) && value is Value.AlText text
            ? text.Item
            : throw new ArgumentException($"View field '{key}' must be a string.");
}
