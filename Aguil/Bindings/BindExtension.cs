using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Aguil.Core;
using Aguil.Core.MCP;
using Aguil.Phlow;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Logging;
using Avalonia.Markup.Xaml;

namespace Aguil.Bindings;

public sealed class BindExtension : ICommand
{
    private static readonly AlMcpClient Client = new();
    private static readonly ObservationCache Observations = new(Client);
    private Control? _control;
    private readonly ReferenceSource _source = new();

    public BindExtension()
    {
    }

    public BindExtension(string path)
    {
        Path = path;
    }

    [AssignBinding]
    public object ObjectId { get; set; } = "";
    public string Path { get; set; } = "";
    public BindingMode Mode { get; set; } = BindingMode.Default;

    public bool CanExecute(object? parameter)
    {
        return _source.Identifier is { Length: > 0 };
    }

    public async void Execute(object? parameter)
    {
        await ExecuteAsync();
    }

    public async Task ExecuteAsync()
    {
        var id = _source.Identifier;
        if (string.IsNullOrEmpty(id)) return;
        try
        {
            var branch = Context.BranchFor(_control!)!;
            var owner = await Observations.Open(id, branch);
            await owner.Pending;
            await Client.RunSlot(id, Path, branch);
        }
        catch (Exception error)
        {
            Logger.TryGet(LogEventLevel.Error, "AL")
                ?.Log(this, "Command {ObjectId}.{Path} failed: {Error}", id, Path, error);
        }
    }

    public event EventHandler? CanExecuteChanged;

    public object ProvideValue(IServiceProvider services)
    {
        if (ObjectId is string id && string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(Path))
            throw new ArgumentException("al:Bind requires ObjectId and Path.");
        var target = (IProvideValueTarget)services.GetService(typeof(IProvideValueTarget))!;
        // This is the entire control object
        var control = (Control)target.TargetObject;
        _control = control;
        _source.PropertyChanged += (_, change) =>
        {
            if (change.Property == ReferenceSource.ObjectIdProperty)
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        };
        if (ObjectId is BindingBase binding)
        {
            _source.Bind(StyledElement.DataContextProperty,
                CompiledBinding.Create<Control, object?>(c => c.DataContext, control));
            _source.Bind(ReferenceSource.ObjectIdProperty, binding);
        }
        else
            _source.ObjectId = ObjectId;

        if (target.TargetProperty is AvaloniaProperty property && property.PropertyType == typeof(ICommand))
        {
            return this;
        }

        var version = 0;
        if (control is ItemsControl items && Equals(target.TargetProperty, ItemsControl.ItemsSourceProperty))
            items.ItemTemplate ??= new FuncDataTemplate<AlValues.AlValue>((item, _) => Item(item!, control));
        async Task Refresh()
        {
            var current = ++version;
            _source.Reference = null;
            var identifier = _source.Identifier;
            if (!control.IsLoaded || string.IsNullOrEmpty(identifier)) return;
            try
            {
                var reference = await Observations.Open(identifier, Path, Context.BranchFor(control)!);
                if (control.IsLoaded && current == version) _source.Reference = reference;
            }
            catch (Exception error)
            {
                Logger.TryGet(LogEventLevel.Error, "AL")?.Log(this, "Loading slot {ObjectId}.{Path} failed: {Error}",
                    identifier, Path, error);
            }
        }

        _source.PropertyChanged += (_, change) =>
        {
            if (change.Property == ReferenceSource.ObjectIdProperty) _ = Refresh();
        };
        control.Loaded += async (_, _) => await Refresh();
        control.Unloaded += (_, _) =>
        {
            ++version;
            _source.Reference = null;
        };
        return CompiledBinding.Create<ReferenceSource, object?>(s => s.Reference!.Value, _source, mode: Mode);
    }

    private static Control Item(AlValues.AlValue item, Control owner)
    {
        var host = new ContentControl();
        host.Loaded += async (_, _) =>
        {
            if (host.Content is not null) return;
            try
            {
                var branch = Context.BranchFor(owner) ?? await Client.HeadBranch();
                host.Content = XamlView.Load(await Client.Xaml(item, branch));
            }
            catch (Exception error)
            {
                Logger.TryGet(LogEventLevel.Error, "AL")?.Log(host, "Rendering {Item} failed: {Error}", item, error);
            }
        };
        return host;
    }
}
