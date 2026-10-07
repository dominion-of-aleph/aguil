using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Metadata;

namespace Aguil.Views;

public partial class InlinePreview : Grid
{
    public InlinePreview()
    {
        InitializeComponent();
    }

    [Content]
    public Control? Content
    {
        get => (Control?)Viewport.Content;
        set => Viewport.Content = value;
    }

    private void Resize(object? sender, VectorEventArgs e)
    {
        var height = Math.Clamp(Viewport.Bounds.Height + e.Vector.Y, 48, 600);
        Viewport.MaxHeight = height;
        Viewport.Height = height;
    }
}