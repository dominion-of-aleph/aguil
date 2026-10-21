using System.Collections;
using Aguil.Core;
using Avalonia.Data.Converters;
using Value = Aguil.Core.AlValues.AlValue;

namespace Aguil.Bindings;

public static class Data
{
    public static readonly IValueConverter Items =
        new FuncValueConverter<Value, IEnumerable?>(value => (value as Value.AlList)?.Item);

    public static readonly IValueConverter Field =
        new FuncValueConverter<Value, string, object?>((item, field) =>
        {
            var value = item is Value.AlMap row ? row.Item[Value.NewAlAtom(field!)] : item;
            return value is Value.AlText text ? text.Item : value;
        });

    public static readonly IValueConverter Target =
        new FuncValueConverter<Value, Value?>(item =>
            item is Value.AlMap row && row.Item.TryGetValue(Value.NewAlAtom("target"), out var target)
                ? target
                : null);
}