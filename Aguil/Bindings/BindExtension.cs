using System;
using System.Windows.Input;
using Aguil.Core;
using Aguil.Core.MCP;
using Aguil.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Logging;
using Avalonia.Markup.Xaml;

namespace Aguil.Bindings;

public sealed class BindExtension : ICommand
{
    private static readonly AlMcpClient Client = new();
    private static readonly Slots Slots = new(Client);
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

        var source = new SlotSource();
        control.Loaded += async (_, _) =>
        {
            try
            {
                source.Slot = await Slots.Open(ObjectId, Path, Inspector.GetInspection(control)?.Branch!);
            }
            catch (Exception error)
            {
                Logger.TryGet(LogEventLevel.Error, "AL")?.Log(this, "Loading slot {ObjectId}.{Path} failed: {Error}",
                    ObjectId, Path, error);
            }
        };
        return CompiledBinding.Create<SlotSource, object?>(s => s.Slot!.Value, source, mode: Mode);
    }

    public bool CanExecute(object? parameter) => true;

    public async void Execute(object? parameter)
    {
        try
        {
            await Client.RunSlot(ObjectId, Path, Inspector.GetInspection(_control!)?.Branch!);
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
}