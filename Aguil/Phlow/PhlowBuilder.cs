using System;
using Aguil.Core;
using Aguil.Views;
using Microsoft.FSharp.Collections;
using Value = Aguil.Core.AlValues.AlValue;

namespace Aguil.Phlow;

public static class PhlowBuilder
{
    public static AlPhlowView Build(FSharpMap<Value, Value> fields)
    {
        var title = AlValues.text("title", fields);
        var priority = fields.TryGetValue(Value.NewAlAtom("priority"), out var p) && p is Value.AlInteger integer
            ? integer.Item
            : throw new ArgumentException("Expected an integer view priority");
        var view = AlValues.text("view", fields) switch
        {
            "aguil_native" => CSharpForward.Resolve(AlValues.text("target", fields)),
            "xaml" => new AlPhlowView(XamlView.Load(AlValues.text("content", fields),
                fields.TryGetValue(Value.NewAlAtom("data"), out var data) ? data : null)),
            "inspector" => new AlPhlowView(new Inspector { Views = AlValues.maps(fields[Value.NewAlAtom("views")]) }),
            var kind => throw new NotSupportedException($"Unknown view: {kind}")
        };
        view.Title = title;
        view.Priority = priority;
        if (fields.TryGetValue(Value.NewAlAtom("target"), out var target))
            InspectionTarget.SetTarget(view.Content, target);
        if (fields.TryGetValue(Value.NewAlAtom("height"), out var height))
            view.Content.Height = height switch
            {
                Value.AlInteger size => (double)size.Item,
                Value.AlFloat number => number.Item,
                _ => double.NaN
            };
        return view;
    }
}