using System;
using Aguil.Core;
using Aguil.Editor;
using Aguil.Phlow;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using AvaloniaEdit;
using Microsoft.FSharp.Collections;
using Value = Aguil.Core.AlValues.AlValue;

namespace Aguil.Elements;

public static class ElementBuilder
{
    public static IRecyclingDataTemplate Template(FSharpMap<Value, Value> fields)
    {
        var field = fields.TryGetValue(Value.NewAlAtom("field"), out var f) && f is Value.AlText name
            ? name.Item : "text";
        var grammar = fields.TryGetValue(Value.NewAlAtom("grammar"), out var g) && g is Value.AlText source
            ? source.Item : null;
        string Display(Value item) => item is Value.AlMap row
            ? AlValues.text(field, row.Item)
            : item is Value.AlText text ? text.Item : item.ToString();

        return AlValues.text("element", fields) switch
        {
            "text" => new Stencil<Value, TextBlock>(() => new TextBlock(), (control, item) =>
            {
                control.Text = Display(item);
                BindTarget(control, item);
            }),
            "editor" => new Stencil<Value, TextEditor>(() => ReadOnlyText.CreateCell(grammar), (control, item) =>
            {
                control.Text = Display(item);
                BindTarget(control, item);
            }),
            var kind => throw new NotSupportedException($"Unknown element: {kind}")
        };
    }

    private static void BindTarget(Control control, Value item) =>
        InspectionTarget.SetTarget(control,
            item is Value.AlMap row && row.Item.TryGetValue(Value.NewAlAtom("target"), out var target)
                ? target : null);
}
