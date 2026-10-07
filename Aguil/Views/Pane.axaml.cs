using System;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace Aguil.Views;

public partial class Pane : HeaderedContentControl
{
    public static readonly StyledProperty<bool> CanCloseProperty =
        AvaloniaProperty.Register<Pane, bool>(nameof(CanClose), true);

    public Pane()
    {
        InitializeComponent();
    }

    public bool CanClose
    {
        get => GetValue(CanCloseProperty);
        set => SetValue(CanCloseProperty, value);
    }

    public PaneColumn? Column => this.FindLogicalAncestorOfType<PaneColumn>();

    public event EventHandler? CloseRequested;

    private void Close(object? sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}