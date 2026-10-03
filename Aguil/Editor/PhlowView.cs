using System;
using Avalonia.Controls;
using AvaloniaEdit;
using Microsoft.FSharp.Collections;
using Value = Aguil.Core.AlValues.AlValue;

namespace Aguil.Editor;

public static class PhlowView
{
    public static Control Render(FSharpMap<Value, Value> fields)
    {
        Control control = Text(fields, "view") switch
        {
            "text" => RenderText(fields),
            var kind => throw new NotSupportedException($"Unknown view: {kind}")
        };
        if (fields.TryGetValue(Value.NewAlAtom("target"), out var target))
            Inspect.SetTarget(control, target);
        if (fields.TryGetValue(Value.NewAlAtom("height"), out var height))
            control.Height = height switch
            {
                Value.AlInteger integer => (double)integer.Item,
                Value.AlFloat number => number.Item,
                _ => double.NaN
            };
        return control;
    }

    public static TextEditor RenderText(FSharpMap<Value, Value> fields)
    {
        var editor = new TextEditor { Text = Text(fields, "text"), IsReadOnly = true };
        Code.SetKeymap(editor, Keymaps.defaults);
        Code.SetGrammar(editor,
            fields.TryGetValue(Value.NewAlAtom("grammar"), out var grammar) && grammar is Value.AlText text
                ? text.Item : null);
        return editor;
    }

    public static string Text(FSharpMap<Value, Value> fields, string key) =>
        ((Value.AlText)fields[Value.NewAlAtom(key)]).Item;
}
