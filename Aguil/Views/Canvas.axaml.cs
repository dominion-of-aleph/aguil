using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using Aguil.Bindings;
using Aguil.Core;
using Aguil.Core.MCP;
using Aguil.Phlow;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Logging;
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
        var viewport = Scroll.Viewport;
        var wm = await WindowManager.Create(al, viewport);
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
        Closed += (_, _) =>
        {
            sizes.Writer.TryComplete();
            wm.Dispose();
        };
        sizes.Writer.TryWrite(Scroll.Viewport);

        await foreach (var size in sizes.Reader.ReadAllAsync())
        {
            if (size == viewport) continue;
            await wm.ViewPortChanged(size);
            viewport = size;
        }
    }
}

public sealed class WindowManager : ObservableObject, IDisposable
{
    private readonly AlMutableObject _reference;
    private Control? _content;
    private bool _disposed;
    private Size _extent;
    private IDisposable? _watch;
    private string? _xaml;

    private WindowManager(AlMutableObject reference)
    {
        _reference = reference;
    }

    public AlValues.AlValue Id => _reference.Identifier;
    public string Branch => _reference.Branch;
    public Task Pending { get; private set; } = Task.CompletedTask;

    public ObservableCollection<Window> Windows { get; } = [];

    public Control? Content
    {
        get => _content;
        private set => SetProperty(ref _content, value);
    }

    public Size Extent
    {
        get => _extent;
        private set => SetProperty(ref _extent, value);
    }

    public void Dispose()
    {
        _disposed = true;
        _watch?.Dispose();
        _watch = null;
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
        return await Attach(al, wm, result.Branch);
    }

    public static async Task<WindowManager> Attach(AlMcpClient al, AlValues.AlValue wm, string branch)
    {
        var reference = await new ObservationCache(al).Open(((AlValues.AlValue.AlAtom)wm).Item, branch);
        var manager = new WindowManager(reference);
        manager._watch = reference.Watch(manager.Changed);
        try
        {
            await manager.Pending;
            return manager;
        }
        catch
        {
            manager.Dispose();
            throw;
        }
    }

    public async Task Spawn(AlPhlowView content, Window? on = null)
    {
        await Spawn(await CSharpForward.ToAl(content, _reference.Client, Branch), on);
    }

    public async Task Spawn(AlValues.AlValue content, Window? on = null)
    {
        var source = on?.Id ?? AlValues.AlValue.NewAlAtom("nil");
        var result = await _reference.Client.QueryAlWith(
            "new_window Wm #{on => On, content => Content} _ _.",
            Branch,
            new Dictionary<string, AlValues.AlValue> { ["Wm"] = Id, ["On"] = source, ["Content"] = content });
        await _reference.Client.ReleaseContext(result.Context);
    }

    public async Task ViewPortChanged(Size size)
    {
        var result = await _reference.Client.QueryAlWith(FormattableString.Invariant(
            $$"""
              new point #{x => {{size.Width}}, y => {{size.Height}}} Visible.
              viewport_changed Wm Visible _ _.
              """), Branch, new Dictionary<string, AlValues.AlValue> { ["Wm"] = Id });
        await _reference.Client.ReleaseContext(result.Context);
    }

    private void Changed(ObjectChange change)
    {
        Pending = ApplyAfter(Pending, change);
        _ = Report(Pending);
    }

    private async Task ApplyAfter(Task previous, ObjectChange change)
    {
        var recover = false;
        try
        {
            await previous;
        }
        catch
        {
            recover = true;
        }

        IEnumerable<Workspace.Update> updates = recover
            ? [Workspace.snapshot(change.Value)]
            : Workspace.changes(change);
        foreach (var update in updates) await Apply(update);
    }

    private async Task Apply(Workspace.Update update)
    {
        if (_disposed) return;

        var prepared = new List<(Workspace.Placement Placement, Window? Existing, AlPhlowView? View)>();
        foreach (var placement in update.Changed)
        {
            var existing = Windows.FirstOrDefault(window => window.Id.Equals(placement.Id));
            var view = existing is null || !Equals(existing.Description, placement.Content)
                ? await ViewFor(placement.Content)
                : null;
            prepared.Add((placement, existing, view));
        }

        var content = _xaml == update.Xaml ? Content : XamlView.Load(update.Xaml, this);
        if (_disposed) return;
        if (content is not null) Context.SetBranch(content, Branch);

        var removed = update.Removed.ToHashSet();
        var retained = update.Changed.Select(window => window.Id).ToHashSet();
        foreach (var window in Windows.Where(window => removed.Contains(window.Id) ||
                                                       (update.Reset && !retained.Contains(window.Id))).ToArray())
            Windows.Remove(window);

        foreach (var (placement, existing, view) in prepared)
        {
            var window = existing ?? new Window(placement.Id);
            window.Update(placement, view);
            if (existing is null) Windows.Add(window);
        }

        Extent = new Size(update.Width, update.Height);
        _xaml = update.Xaml;
        Content = content;
    }

    private async Task Report(Task pending)
    {
        try
        {
            await pending;
        }
        catch (Exception error)
        {
            Logger.TryGet(LogEventLevel.Error, "AL")?.Log(this, "Workspace update failed: {Error}", error);
        }
    }

    private async Task<AlPhlowView> ViewFor(AlValues.AlValue content)
    {
        var result = await _reference.Client.QueryAlWith("to_map Content Description.", Branch,
            new Dictionary<string, AlValues.AlValue> { ["Content"] = content });
        var description = (AlValues.AlValue.AlMap)Evaluation.binding("Description", result);
        await _reference.Client.ReleaseContext(result.Context);
        var view = PhlowBuilder.Build(description.Item);
        Context.SetBranch(view.Content, Branch);
        return view;
    }
}

public sealed class Window(AlValues.AlValue id) : ObservableObject
{
    private Control? _content;
    private Rect _rect;
    private string _title = "";
    public AlValues.AlValue Id { get; } = id;
    internal AlValues.AlValue? Description { get; private set; }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public Control? Content
    {
        get => _content;
        private set => SetProperty(ref _content, value);
    }

    public Rect Rect
    {
        get => _rect;
        private set => SetProperty(ref _rect, value);
    }

    internal void Update(Workspace.Placement placement, AlPhlowView? view)
    {
        Rect = new Rect(placement.X, placement.Y, placement.Width, placement.Height);
        Description = placement.Content;
        if (view is null) return;
        Title = view.Title;
        Content = view.Content;
    }
}
