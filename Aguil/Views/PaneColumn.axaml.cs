using System;
using System.Collections.Specialized;
using System.Linq;
using Aguil.Core;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Metadata;

namespace Aguil.Views;

public partial class PaneColumn : Decorator
{
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<PaneColumn, bool>(nameof(IsExpanded));

    public PaneColumn()
    {
        InitializeComponent();
        Panes.Validate += control =>
        {
            if (control is not Pane)
                throw new ArgumentException("A pane column accepts panes.", nameof(control));
        };
        Panes.ResetBehavior = ResetBehavior.Remove;
        Panes.CollectionChanged += Changed;
        SizeChanged += (_, _) =>
        {
            foreach (var row in PaneGrid.RowDefinitions)
                row.Height = new GridLength(Bounds.Height);
        };
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    [Content] public Controls Panes => PaneGrid.Children;

    private void ContainScroll(object? sender, RequestBringIntoViewEventArgs e) => e.Handled = true;

    private void Changed(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Move) return;
        if (e.OldItems is not null)
            foreach (Pane pane in e.OldItems)
                pane.CloseRequested -= CloseRequested;

        if (e.NewItems is not null)
        {
            var occupied = Panes.Except(e.NewItems.Cast<Control>()).Select(Grid.GetRow).ToHashSet();
            for (var i = 0; i < e.NewItems.Count; i++)
            {
                var pane = (Pane)e.NewItems[i]!;
                var start = e.Action == NotifyCollectionChangedAction.Replace
                    ? Grid.GetRow((Pane)e.OldItems![i]!)
                    : Grid.GetRow(pane);
                var row = Layout.vacant(occupied, start);
                occupied.Add(row);
                Place(pane, row);
                pane.CloseRequested += CloseRequested;
            }
        }
    }

    private void CloseRequested(object? sender, EventArgs e) => Panes.Remove((Pane)sender!);

    internal void Move(Pane pane, int row) =>
        Place(pane, Layout.vacant(Panes.Where(p => p != pane).Select(Grid.GetRow), row));

    private void Place(Pane pane, int row)
    {
        while (PaneGrid.RowDefinitions.Count <= row)
            PaneGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(Bounds.Height) });
        Grid.SetRow(pane, row);
    }
}