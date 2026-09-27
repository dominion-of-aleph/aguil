using System.Collections.ObjectModel;
using System.Threading.Tasks;
using AvaloniaPlayground.Core;
using AvaloniaPlayground.ViewModels;
using AvaloniaPlayground.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaPlayground.ViewModels;

public partial class ReplViewModel : ViewModelBase
{
    private readonly AlMcpClient _al = new();

    public ObservableCollection<ReplEntry> History { get; } = [];

    [ObservableProperty]
    public partial string Source { get; set; } = "";

    [RelayCommand]
    private async Task Run()
    {
        var source = Source;
        Source = "";
        // Replace with a real sum type logic
        try
        {
            var res = await _al.QueryAl(source, null);
            History.Add(new ReplEntry(source, res.ToString()));
        }
        catch (AlException e)
        {
            History.Add(new ReplEntry(source, e.Message));
        }
    }
    
}