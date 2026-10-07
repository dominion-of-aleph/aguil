using System;
using Aguil.Core;
using Aguil.Editor;
using Aguil.Phlow;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using AvaloniaEdit;
using Microsoft.FSharp.Collections;
using Value = Aguil.Core.AlValues.AlValue;

namespace Aguil.Elements;

public static class ElementBuilder
{
    public static IRecyclingDataTemplate Template(FSharpMap<Value, Value> fields)
    {
        var field = fields.TryGetValue(Value.NewAlAtom("field"), out var f) && f is Value.AlText name
            ? name.Item
            : "text";
        var grammar = fields.TryGetValue(Value.NewAlAtom("grammar"), out var g) && g is Value.AlText source
            ? source.Item
            : null;

        string Display(Value item)
        {
            var value = item is Value.AlMap row ? row.Item[Value.NewAlAtom(field)] : item;
            return value is Value.AlText text ? text.Item : value.ToString();
        }

        return AlValues.text("element", fields) switch
        {
            "text" => new Stencil<Value, TextBlock>(() => new TextBlock { Background = Brushes.Transparent },
                (control, item) =>
                {
                    control.Text = Display(item);
                    InspectionTarget.SetTarget(control, Target(item));
                }),
            "editor" => new Stencil<Value, TextEditor>(() => ReadOnlyText.CreateCell(grammar), (control, item) =>
            {
                control.Text = Display(item);
                InspectionTarget.SetTarget(control, Target(item));
            }),
            var kind => throw new NotSupportedException($"Unknown element: {kind}")
        };
    }

    public static Value? Target(Value? item)
    {
        return item is Value.AlMap row && row.Item.TryGetValue(Value.NewAlAtom("target"), out var target)
            ? target
            : null;
    }
}