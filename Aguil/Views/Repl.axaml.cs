using System;
using System.Linq;
using Aguil.Core.MCP;
using Aguil.Editor;
using Aguil.Phlow;
using Value = Aguil.Core.AlValues.AlValue;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aguil.ViewModels;

namespace Aguil.Views;

public partial class Repl : Window
{
    public Repl()
    {
        InitializeComponent();
        var vm = new ReplViewModel();
        DataContext = vm;
        _ = vm.LoadPreviousInputs();

        Input.TextChanged += (_, _) =>
        {
            vm.Source = Input.Text;
            ScrollToEnd();
        };
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.Source) && Input.Text != vm.Source)
            {
                Input.Text = vm.Source;
                // a recalled input is edited from its end
                Input.CaretOffset = Input.Text.Length;
            }
        };

        Input.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.Control)
            {
                vm.RunCommand.Execute(null);
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);

        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.KeyModifiers != KeyModifiers.Alt && e.KeyModifiers != (KeyModifiers.Control | KeyModifiers.Alt))
                return;
            var index = (int)e.Key - (int)Key.D1;
            if (index is < 0 or > 8 || vm.Target is not { } target) return;
            var level = e.KeyModifiers.HasFlag(KeyModifiers.Control) ? 1 : 0;
            var tabs = Entries.GetVisualDescendants().OfType<Inspector>().Where(t =>
                    t.IsEffectivelyVisible &&
                    t.GetVisualAncestors().Prepend(t).OfType<Control>().Any(c => c.DataContext == target))
                .ElementAtOrDefault(level);
            if (tabs is null || index >= tabs.Items.Count) return;
            tabs.SelectedIndex = index;
            e.Handled = true;
        }, RoutingStrategies.Tunnel);

        vm.History.CollectionChanged += (_, _) => ScrollToEnd();

        var al = new AlMcpClient();

        Flow.AddHandler(PointerPressedEvent, async (_, e) =>
        {
            if (e.Source is Control source &&
                e.GetCurrentPoint(source).Properties.PointerUpdateKind
                == PointerUpdateKind.MiddleButtonPressed &&
                InspectionTarget.GetTarget(source) is Value target)
            {
                e.Handled = true;

                try
                {
                    var child = new Inspector { Views = await al.Inspect(target, null) };
                    Flow.Children.Add(child);
                    Dispatcher.UIThread.Post(child.BringIntoView, DispatcherPriority.Loaded);
                }
                catch (Exception error)
                {
                    Flow.Children.Add(ReadOnlyText.Create(error.Message));
                }
            }
        }, RoutingStrategies.Tunnel);

        // the editors handle the press (to place the caret), so ListBox never selects
        Entries.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (e.Source is Control source && source.GetVisualAncestors().Prepend(source)
                    .OfType<Control>().Select(control => control.DataContext)
                    .OfType<ReplEntry>().FirstOrDefault() is { } entry)
                vm.Selected = entry;
        }, handledEventsToo: true);
    }


    private async void CopyResult(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ReplSuccess entry } && Clipboard is { } clipboard)
            await clipboard.SetTextAsync(entry.Result.Pretty(80));
    }

    // the new content has not been laid out yet when these fire
    private void ScrollToEnd() =>
        Dispatcher.UIThread.Post(Scroll.ScrollToEnd, DispatcherPriority.Background);
}
