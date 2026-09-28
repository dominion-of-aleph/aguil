using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AvaloniaPlayground.ViewModels;

namespace AvaloniaPlayground.Views;

public partial class Repl : Window
{
    public Repl()
    {
        InitializeComponent();
        var vm = new ReplViewModel();
        DataContext = vm;

        Input.TextChanged += (_, _) => vm.Source = Input.Text;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.Source) && Input.Text != vm.Source)
                Input.Text = vm.Source;
        };

        Input.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.Control)
            {
                vm.RunCommand.Execute(null);
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);

        vm.History.CollectionChanged += (_, _) => Scroll.ScrollToEnd();
    }
}