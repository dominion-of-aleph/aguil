using System;
using System.Linq;
using System.Threading.Tasks;
using Aguil.Core;
using Aguil.Core.MCP;
using Aguil.Views;
using Avalonia.Controls;
using Microsoft.FSharp.Collections;
using Value = Aguil.Core.AlValues.AlValue;

namespace Aguil.Phlow;

public static class PhlowBuilder
{
    private static readonly AlMcpClient Client = new();

    public static AlPhlowView Build(FSharpMap<Value, Value> fields)
    {
        var title = AlValues.text("title", fields);
        var priority = fields.TryGetValue(Value.NewAlAtom("priority"), out var p) && p is Value.AlInteger integer
            ? integer.Item
            : throw new ArgumentException("Expected an integer view priority");
        var content = new DeferredContent(Factory(fields));
        Configure(content, fields);
        return new AlPhlowView(content) { Title = title, Priority = priority };
    }

    private static Func<Task<Control>> Factory(FSharpMap<Value, Value> fields)
    {
        return AlValues.text("view", fields) switch
        {
            "aguil_native" => () => Task.FromResult(CSharpForward.Resolve(AlValues.text("target", fields)).Content),
            "xaml" => () => Task.FromResult(XamlView.Load(AlValues.text("content", fields),
                fields.TryGetValue(Value.NewAlAtom("data"), out var data) ? data : null)),
            "inspector" => () => Task.FromResult<Control>(new Inspector
            { Views = AlValues.maps(fields[Value.NewAlAtom("views")]).Select(view => InheritBranch(fields, view)) }),
            "deferred" or "inspect" => () => Resolve(fields),
            var kind => throw new NotSupportedException($"Unknown view: {kind}")
        };
    }

    private static FSharpMap<Value, Value> InheritBranch(FSharpMap<Value, Value> parent, FSharpMap<Value, Value> child)
    {
        var key = Value.NewAlAtom("branch");
        return !child.ContainsKey(key) && parent.TryGetValue(key, out var branch) ? child.Add(key, branch) : child;
    }

    private static async Task<Control> Resolve(FSharpMap<Value, Value> fields)
    {
        var description = await Client.ResolveView(fields);
        var content = await Factory(description)();
        Configure(content, description);
        return content;
    }

    private static void Configure(Control content, FSharpMap<Value, Value> fields)
    {
        if (fields.TryGetValue(Value.NewAlAtom("branch"), out var branch) && branch is Value.AlText name)
            Bindings.Context.SetBranch(content, name.Item);
        if (fields.TryGetValue(Value.NewAlAtom("target"), out var target))
            InspectionTarget.SetTarget(content, target);
        if (fields.TryGetValue(Value.NewAlAtom("height"), out var height))
            content.Height = height switch
            {
                Value.AlInteger size => (double)size.Item,
                Value.AlFloat number => number.Item,
                _ => double.NaN
            };
    }
}
