using Avalonia;
using Avalonia.Controls;
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
        vm.History.CollectionChanged += (_, _) => Scroll.ScrollToEnd();
    }
}