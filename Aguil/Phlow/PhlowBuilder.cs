using System;
using System.Linq;
using System.Numerics;
using Aguil.Core;
using Aguil.Elements;
using Avalonia.Controls;
using Microsoft.FSharp.Collections;
using Value = Aguil.Core.AlValues.AlValue;

namespace Aguil.Phlow;

public static class PhlowBuilder
{
    public static AlPhlowView Build(FSharpMap<Value, Value> fields)
    {
        var title = "View error";
        BigInteger priority = 100;
        try
        {
            title = AlValues.text("title", fields);
            priority = fields.TryGetValue(Value.NewAlAtom("priority"), out var p) && p is Value.AlInteger integer
                ? integer.Item
                : throw new ArgumentException("Expected an integer view priority");
            var grammar = fields.TryGetValue(Value.NewAlAtom("grammar"), out var g) && g is Value.AlText source
                ? source.Item
                : null;
            AlPhlowView view = AlValues.text("view", fields) switch
            {
                "text" => new TextView(AlValues.text("text", fields), grammar),
                "columned_list" => new ColumnedList(((Value.AlList)fields[Value.NewAlAtom("items")]).Item,
                    AlValues.maps(fields[Value.NewAlAtom("columns")]).Select(Column)),
                "inspector" => new EmbeddedInspector(AlValues.maps(fields[Value.NewAlAtom("views")])),
                "aguil_raw" => new TextView(fields[Value.NewAlAtom("target")].ToString(), "elixir"),
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
        catch (Exception error)
        {
            return new TextView(error.Message) { Title = title, Priority = priority };
        }
    }

    private static TableViewColumn Column(FSharpMap<Value, Value> fields)
    {
        return new TableViewColumn
        {
            Header = fields.TryGetValue(Value.NewAlAtom("title"), out var title) && title is Value.AlText text
                ? text.Item
                : null,
            CellTemplate = ElementBuilder.Template(fields),
            Width = GridLength.Auto
        };
    }
}