using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Logging;
using Avalonia.Media;

namespace Aguil.Views;

public sealed class DeferredContent : ContentControl
{
    private readonly Func<Task<Control>> _create;
    private Task<Control>? _pending;

    public DeferredContent(Func<Task<Control>> create)
    {
        _create = create;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
    }

    public Task<Control> Load() => _pending ??= _create();

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (Content is not null) return;
        Content = new TextBlock { Text = "Loading…" };
        try
        {
            Content = await Load();
        }
        catch (Exception error)
        {
            Logger.TryGet(LogEventLevel.Error, "AL")?.Log(this, "View failed: {Error}", error);
            Content = new TextBlock { Text = error.Message, TextWrapping = TextWrapping.Wrap };
        }
    }
}
