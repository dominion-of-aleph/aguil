using System.Runtime.InteropServices.Swift;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AvaloniaPlayground.Views;

public partial class MainWindow : Window
{
    public static double OldFahrenheit { get; set; }
    public static double OldCelsius { get; set; }

    public MainWindow()
    {
        InitializeComponent();
    }

    public void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        var cb = double.TryParse(Celsius.Text, out var c);
        var fb = double.TryParse(Fahrenheit.Text, out var f);

        var (cr, fr) = (CB: cb, FB: fb) switch
        {
            (false, _) or (_, false) => (0d, 0d),
            _ when f == OldFahrenheit => (c, c * (9d / 5) + 32),
            // fallback
            _ => ((f - 32) * (5d / 9), f)
        };
        OldCelsius = cr;
        OldFahrenheit = fr;
        Fahrenheit.Text = fr.ToString("0.0");
        Celsius.Text = cr.ToString("0.0");
    }

    private void Celsius_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        Button_OnClick(sender, e);
    }

    private void Fahrenheit_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        Button_OnClick(sender, e);
    }
}