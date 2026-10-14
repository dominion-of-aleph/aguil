using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using Aguil.Core;
using Aguil.Core.MCP;
using Aguil.Phlow;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aguil.Views;

public partial class Canvas : Avalonia.Controls.Window
{
    public Canvas()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs eventArgs)
    {
        Opened -= OnOpened;
        UpdateLayout();

        var al = new AlMcpClient();
        var wm = await WindowManager.Create(al, Scroll.Viewport);
        DataContext = wm;
        await wm.Spawn(new CSharpForward(new Repl()) { Title = "REPL" });

        var sizes = Channel.CreateBounded<Size>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        Scroll.PropertyChanged += (_, change) =>
        {
            if (change.Property == ScrollViewer.ViewportProperty)
                sizes.Writer.TryWrite(Scroll.Viewport);
        };
        Closed += (_, _) => sizes.Writer.TryComplete();
        sizes.Writer.TryWrite(Scroll.Viewport);

        await foreach (var size in sizes.Reader.ReadAllAsync())
            await wm.ViewPortChanged(size);
    }
}

public sealed class WindowManager(AlMcpClient al, AlValues.AlValue wm) : ObservableObject
{
    private Size _extent;

    public ObservableCollection<Window> Windows { get; } = [];

    public Size Extent
    {
        get => _extent;
        private set => SetProperty(ref _extent, value);
    }

    public static async Task<WindowManager> Create(AlMcpClient al, Size extent)
    {
        // Floating points on euro macchines are like 12,5
        var result = await al.QueryAl(FormattableString.Invariant(
            $$"""
              new(:point, %{x: {{extent.Width}}, y: {{extent.Height}}}, visible_extent)
              new(:point, %{x: 0, y: 0}, total_extent)
              new(:single_layout, %{windows: []}, layout)
              new(:wm, %{layout: layout, visible_extent: visible_extent, total_extent: total_extent}, wm)
              """), null);
        return new WindowManager(al, Evaluation.binding("wm", result));
    }

    public async Task Spawn(AlPhlowView content, Window? on = null)
    {
        await Spawn(await CSharpForward.ToAl(content, al), on);
    }

    public async Task Spawn(AlValues.AlValue content, Window? on = null)
    {
        var source = on?.Id ?? AlValues.AlValue.NewAlAtom("nil");
        var result = await al.QueryAl(
            $$"""
              new_window({{AlValues.AlValue.Flat(wm)}},
                  %{on: {{AlValues.AlValue.Flat(source)}}, content: {{AlValues.AlValue.Flat(content)}}},
                  changed, removed)
              get_slots({{AlValues.AlValue.Flat(wm)}}, %{total_extent: total_extent})
              """, null);
        await Show(result);
    }

    public async Task ViewPortChanged(Size size)
    {
        var result = await al.QueryAl(FormattableString.Invariant(
            $$"""
              new(:point, %{x: {{size.Width}}, y: {{size.Height}}}, visible)
              viewport_changed({{AlValues.AlValue.Flat(wm)}}, visible, changed, removed)
              get_slots({{AlValues.AlValue.Flat(wm)}}, %{total_extent: total_extent})
              """), null);
        await Show(result);
    }

    public void Delete(Window window)
    {
        // Call Layout, through WM
    }

    public void Focus(Window window)
    {
        // Call WM
    }

    public void Reveal(Window window)
    {
        // Bring into view, but not focus
    }

    public void Move(Window window)
    {
        // Direction needed as well
    }

    public void Resize(Window window, Size size)
    {
        // Go into the WM
    }

    private async Task Show(Evaluation.EvaluationContext result)
    {
        var removedIds = ((AlValues.AlValue.AlList)Evaluation.binding("removed", result)).Item
            .Select(window => AlValues.field("id", window)).ToHashSet();
        var removed = Windows.Where(window => removedIds.Contains(window.Id)).ToArray();

        foreach (var window in removed)
            Windows.Remove(window);

        var changed = ((AlValues.AlValue.AlList)Evaluation.binding("changed", result)).Item;
        foreach (var change in changed)
        {
            var id = AlValues.field("id", change);
            var existing = Windows.FirstOrDefault(window => window.Id.Equals(id));
            var rect = WindowRect(change);

            if (existing is not null)
            {
                existing.Rect = rect;
            }
            else
            {
                var content = AlValues.field("content", change);
                var view = await ViewFor(content);
                Windows.Add(new Window(view.Title, view.Content, rect, id));
            }
        }

        Extent = WindowSize(Evaluation.binding("total_extent", result));
    }

    private async Task<AlPhlowView> ViewFor(AlValues.AlValue content)
    {
        var result = await al.QueryAl(
            $"to_map({AlValues.AlValue.Flat(content)}, description)", null);
        var description = (AlValues.AlValue.AlMap)Evaluation.binding("description", result);
        return PhlowBuilder.Build(description.Item);
    }

    private static Size WindowSize(AlValues.AlValue extent)
    {
        return new Size(AlValues.number(AlValues.field("x", extent)), AlValues.number(AlValues.field("y", extent)));
    }

    private static Rect WindowRect(AlValues.AlValue window)
    {
        var position = AlValues.field("position", window);
        return new Rect(
            new Point(AlValues.number(AlValues.field("x", position)), AlValues.number(AlValues.field("y", position))),
            WindowSize(AlValues.field("extent", window)));
    }
}

public sealed class Window(string title, Control content, Rect rect, AlValues.AlValue id) : ObservableObject
{
    private Rect _rect = rect;
    public string Title { get; } = title;
    public Control Content { get; } = content;
    public AlValues.AlValue Id { get; } = id;

    public Rect Rect
    {
        get => _rect;
        set => SetProperty(ref _rect, value);
    }
}