using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaEdit.TextMate;
using AvaloniaPlayground.ViewModels;
using TextMateSharp.Grammars;

namespace AvaloniaPlayground.Views;

public partial class Repl : Window
{
    public Repl()
    {
        InitializeComponent();
        var vm = new ReplViewModel();
        DataContext = vm;

        Input.TextChanged += (_, _) =>
        {
            vm.Source = Input.Text;
            ScrollToEnd();
        };
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

        vm.History.CollectionChanged += (_, _) => ScrollToEnd();

        var textMateOptions = new RegistryOptions(ThemeName.DarkPlus);
        var textMate = Input.InstallTextMate(textMateOptions);
        textMate.SetGrammarFile(Path.Combine(AppContext.BaseDirectory, "Grammars", "elixir.json"));
        ActualThemeVariantChanged += (_, _) => ApplyTheme();
        ApplyTheme();

        void ApplyTheme() =>
            textMate.SetTheme(textMateOptions.LoadTheme(
                ActualThemeVariant == ThemeVariant.Dark ? ThemeName.DarkPlus : ThemeName.LightPlus));
    }

    // the new content has not been laid out yet when these fire
    private void ScrollToEnd() =>
        Dispatcher.UIThread.Post(Scroll.ScrollToEnd, DispatcherPriority.Background);
}