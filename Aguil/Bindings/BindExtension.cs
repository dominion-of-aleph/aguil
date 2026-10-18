using System;
using Aguil.Core;
using Aguil.Core.MCP;
using Aguil.Views;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace Aguil.Bindings;

public sealed class BindExtension
{
    private static readonly Slots Slots = new(new AlMcpClient());

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

        var source = new SlotSource();
        control.Loaded += async (_, _) =>
        {
            try
            {
                var slot = await Slots.Open(ObjectId, Path, Inspector.GetInspection(control)?.Branch!);
                DataValidationErrors.SetErrors(control, null);
                source.Slot = slot;
            }
            catch (Exception error)
            {
                DataValidationErrors.SetErrors(control, new[] { error });
            }
        };
        return CompiledBinding.Create<SlotSource, object?>(s => s.Slot!.Value, source, mode: Mode);
    }
}