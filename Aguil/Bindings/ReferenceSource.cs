using Aguil.Core;
using Avalonia;

namespace Aguil.Bindings;

public sealed class ReferenceSource : StyledElement
{
    public static readonly StyledProperty<object?> ObjectIdProperty =
        AvaloniaProperty.Register<ReferenceSource, object?>(nameof(ObjectId));

    public static readonly StyledProperty<AlMutableObjectSlot?> ReferenceProperty =
        AvaloniaProperty.Register<ReferenceSource, AlMutableObjectSlot?>(nameof(Reference));

    public object? ObjectId
    {
        get => GetValue(ObjectIdProperty);
        set => SetValue(ObjectIdProperty, value);
    }

    public AlMutableObjectSlot? Reference
    {
        get => GetValue(ReferenceProperty);
        set => SetValue(ReferenceProperty, value);
    }

    public string? Identifier => ObjectId switch
    {
        string id => id,
        AlValues.AlValue.AlAtom atom => atom.Item,
        _ => null
    };
}
