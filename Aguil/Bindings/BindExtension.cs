using System;
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

    public BindExtension()
    {
    }

    public BindExtension(string path)
    {
        Path = path;
    }

    public string ObjectId { get; set; } = "";
    public string Path { get; set; } = "";
    public BindingMode Mode { get; set; } = BindingMode.Default;

    public bool CanExecute(object? parameter)
    {
        return true;
    }

    public async void Execute(object? parameter)
    {
        try
        {
            await Client.RunSlot(ObjectId, Path, Context.BranchFor(_control!)!);
        }
        catch (Exception error)
        {
            Logger.TryGet(LogEventLevel.Error, "AL")
                ?.Log(this, "Command {ObjectId}.{Path} failed: {Error}", ObjectId, Path, error);
        }
    }

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public object ProvideValue(IServiceProvider services)
    {
        if (string.IsNullOrWhiteSpace(ObjectId) || string.IsNullOrWhiteSpace(Path))
            throw new ArgumentException("al:Bind requires ObjectId and Path.");
        var target = (IProvideValueTarget)services.GetService(typeof(IProvideValueTarget))!;
        // This is the entire control object
        var control = (Control)target.TargetObject;

        if (target.TargetProperty is AvaloniaProperty property && property.PropertyType == typeof(ICommand))
        {
            _control = control;
            return this;
        }

        var source = new ReferenceSource();
        if (control is ItemsControl items && Equals(target.TargetProperty, ItemsControl.ItemsSourceProperty))
            items.ItemTemplate ??= new FuncDataTemplate<AlValues.AlValue>((item, _) => Item(item!, control));
        control.Loaded += async (_, _) =>
        {
            try
            {
                var reference = await Observations.Open(ObjectId, Path, Context.BranchFor(control)!);
                if (control.IsLoaded) source.Reference = reference;
            }
            catch (Exception error)
            {
                Logger.TryGet(LogEventLevel.Error, "AL")?.Log(this, "Loading slot {ObjectId}.{Path} failed: {Error}",
                    ObjectId, Path, error);
            }
        };
        control.Unloaded += (_, _) => source.Reference = null;
        return CompiledBinding.Create<ReferenceSource, object?>(s => s.Reference!.Value, source, mode: Mode);
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