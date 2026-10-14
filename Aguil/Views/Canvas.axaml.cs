using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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

        Scroll.PropertyChanged += async (_, change) =>
        {
            if (change.Property == ScrollViewer.ViewportProperty)
                await wm.ViewPortChanged(Scroll.Viewport);
        };
        await wm.ViewPortChanged(Scroll.Viewport);
    }
}

public sealed class WindowManager(AlMcpClient al, AlValues.AlValue wm, string branch) : ObservableObject
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
              new point #{x => {{extent.Width}}, y => {{extent.Height}}} VisibleExtent.
              new point #{x => 0, y => 0} TotalExtent.
              new single_layout #{windows => []} Layout.
              new wm #{layout => Layout, visible_extent => VisibleExtent, total_extent => TotalExtent} Wm.
              """), null);
        var wm = Evaluation.binding("Wm", result);
        await al.ReleaseContext(result.Context);
        return new WindowManager(al, wm, result.Branch);
    }

    public async Task Spawn(AlPhlowView content, Window? on = null)
    {
        await Spawn(await CSharpForward.ToAl(content, al, branch), on);
    }

    public async Task Spawn(AlValues.AlValue content, Window? on = null)
    {
        var source = on?.Id ?? AlValues.AlValue.NewAlAtom("nil");
        var result = await al.QueryAlWith(
            "new_window Wm #{on => On, content => Content} Changed Removed. get_slots Wm #{total_extent => TotalExtent}.",
            branch,
            new Dictionary<string, AlValues.AlValue> { ["Wm"] = wm, ["On"] = source, ["Content"] = content });
        try { await Show(result); }
        finally { await al.ReleaseContext(result.Context); }
    }

    public async Task ViewPortChanged(Size size)
    {
        var result = await al.QueryAlWith(FormattableString.Invariant(
            $$"""
              new point #{x => {{size.Width}}, y => {{size.Height}}} Visible.
              viewport_changed Wm Visible Changed Removed.
              get_slots Wm #{total_extent => TotalExtent}.
              """), branch, new Dictionary<string, AlValues.AlValue> { ["Wm"] = wm });
        try { await Show(result); }
        finally { await al.ReleaseContext(result.Context); }
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
        var removedIds = ((AlValues.AlValue.AlList)Evaluation.binding("Removed", result)).Item
            .Select(window => AlValues.field("id", window)).ToHashSet();
        var removed = Windows.Where(window => removedIds.Contains(window.Id)).ToArray();

        foreach (var window in removed)
            Windows.Remove(window);

        var changed = ((AlValues.AlValue.AlList)Evaluation.binding("Changed", result)).Item;
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

        Extent = WindowSize(Evaluation.binding("TotalExtent", result));
    }

    private async Task<AlPhlowView> ViewFor(AlValues.AlValue content)
    {
        var result = await al.QueryAlWith("to_map Content Description.", branch,
            new Dictionary<string, AlValues.AlValue> { ["Content"] = content });
        var description = (AlValues.AlValue.AlMap)Evaluation.binding("Description", result);
        await al.ReleaseContext(result.Context);
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
