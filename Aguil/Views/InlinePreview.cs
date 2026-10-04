using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Metadata;

namespace Aguil.Views;

public class InlinePreview : Grid
{
    private readonly ContentControl _viewport = new() { MaxHeight = 240 };

    [Content]
    public Control? Content
    {
        get => (Control?)_viewport.Content;
        set => _viewport.Content = value;
    }

    public InlinePreview()
    {
        RowDefinitions = new RowDefinitions("Auto,Auto");
        var grip = new Thumb
        {
            Classes = { "preview-resize" },
            Cursor = new Cursor(StandardCursorType.SizeNorthSouth),
            [RowProperty] = 1,
            [ToolTip.TipProperty] = "Drag to resize preview"
        };
        grip.DragDelta += (_, e) =>
        {
            var height = Math.Clamp(_viewport.Bounds.Height + e.Vector.Y, 48, 600);
            _viewport.MaxHeight = height;
            _viewport.Height = height;
        };
        Children.Add(_viewport);
        Children.Add(grip);
    }
}