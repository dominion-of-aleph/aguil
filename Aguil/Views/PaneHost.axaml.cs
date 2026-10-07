using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Aguil.Views;

public partial class PaneHost : Decorator
{
    public PaneHost()
    {
        InitializeComponent();
        Columns.Validate += control =>
        {
            if (control is not PaneColumn)
                throw new ArgumentException("A pane host accepts columns.", nameof(control));
        };
        Columns.ResetBehavior = ResetBehavior.Remove;
        Columns.CollectionChanged += ColumnsChanged;
        SizeChanged += (_, _) => Allocate();
        AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (e.Source is Control control &&
                control.GetVisualAncestors().Prepend(control).OfType<Pane>().FirstOrDefault(Panes.Contains) is { } pane)
                Select(pane);
        }, RoutingStrategies.Tunnel);
    }

    [Content] public Controls Columns => ColumnStrip.Children;

    public IEnumerable<Pane> Panes => Columns.Cast<PaneColumn>().SelectMany(column => column.Panes.Cast<Pane>());

    public Pane? Selected { get; private set; }

    public void Open(Pane pane, Pane source)
    {
        if (!Panes.Contains(source))
            throw new ArgumentException("The source must belong to this host.", nameof(source));
        source.Column!.IsExpanded = false;
        Column(0).IsExpanded = false;
        Grid.SetRow(pane, 0);
        var column = new PaneColumn();
        column.Panes.Add(pane);
        Columns.Insert(1, column);
    }

    public void Move(Pane pane, int row, int column)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        Select(pane);
        var target = Column(column);
        if (pane.Column == target)
        {
            target.Move(pane, row);
            Reveal(pane);
        }
        else
        {
            pane.Column!.Panes.Remove(pane);
            Grid.SetRow(pane, row);
            target.Panes.Add(pane);
        }
    }

    public void Close(Pane pane)
    {
        if (Panes.Contains(pane)) pane.Column!.Panes.Remove(pane);
    }

    public void Select(Pane? pane)
    {
        if (pane is not null && !Panes.Contains(pane))
            throw new ArgumentException("Selection must belong to this host.", nameof(pane));
        Selected?.Classes.Remove("selected");
        Selected = pane;
        Selected?.Classes.Add("selected");
    }

    private void ColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Move) return;
        if (e.OldItems is not null)
            foreach (PaneColumn column in e.OldItems)
            {
                column.PropertyChanged -= ColumnChanged;
                column.Panes.CollectionChanged -= PanesChanged;
            }

        if (e.NewItems is not null)
            foreach (PaneColumn column in e.NewItems)
            {
                column.PropertyChanged += ColumnChanged;
                column.Panes.CollectionChanged += PanesChanged;
            }

        Allocate();
        UpdateSelection(
            e.NewItems?.Cast<PaneColumn>().SelectMany(column => column.Panes).OfType<Pane>().LastOrDefault());
    }

    private void PanesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (var column in Columns.Cast<PaneColumn>().Skip(1)
                         .Where(column => column.Panes.Count == 0 && ReferenceEquals(column.Panes, sender)).ToArray())
                Columns.Remove(column);
        if (e.Action != NotifyCollectionChangedAction.Move)
            UpdateSelection(e.NewItems?.Cast<Pane>().LastOrDefault());
    }

    private void UpdateSelection(Pane? added)
    {
        if (added is not null)
        {
            Select(added);
            Reveal(added);
        }
        else if (Selected is { } selected && !Panes.Contains(selected))
            Select(Panes.LastOrDefault());
    }

    private PaneColumn Column(int index)
    {
        while (Columns.Count <= index) Columns.Add(new PaneColumn());
        return (PaneColumn)Columns[index];
    }

    private void Allocate()
    {
        var width = Columns.Count > 1
            ? Math.Max(0, (Bounds.Width - ColumnStrip.Spacing) / 2)
            : Bounds.Width;
        foreach (PaneColumn column in Columns)
        {
            column.Width = column.IsExpanded ? Bounds.Width : width;
            column.Height = Bounds.Height;
        }
    }

    private void ColumnChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == PaneColumn.IsExpandedProperty)
        {
            Allocate();
            if (Selected is { } pane) Reveal(pane);
        }
    }

    private void Reveal(Pane pane)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (Panes.Contains(pane))
            {
                Scroll.UpdateLayout();
                pane.BringIntoView();
                pane.Column!.BringIntoView();
            }
        }, DispatcherPriority.Loaded);
    }
}