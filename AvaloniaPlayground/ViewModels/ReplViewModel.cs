using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AvaloniaPlayground.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaPlayground.ViewModels;

public partial class ReplViewModel : ViewModelBase
{
    private readonly AlMcpClient _al = new();

    public ObservableCollection<ReplEntry> History { get; } = [];

    [ObservableProperty] public partial string Source { get; set; } = "";

    [RelayCommand]
    private async Task Run()
    {
        var source = Source;
        Source = "";
        ReplEntry entry;
        // Replace with a real sum type logic
        try
        {
            var res = await _al.QueryAl(source, null);
            entry = new ReplSuccess(source, res, _al);
        }
        catch (AlException e)
        {
            entry = new ReplFailure(source, e.Message);
        }

        History.Add(entry);
        Selected = entry;
    }

    [ObservableProperty] public partial ReplEntry? Selected { get; set; }

    private ReplSuccess? Target =>
        Selected switch
        {
            ReplSuccess s => s,
            null => History.OfType<ReplSuccess>().LastOrDefault(),
            _ => null
        };

    [RelayCommand]
    private Task NextSolution() => Target?.NextCommand.ExecuteAsync(null) ?? Task.CompletedTask;

    [RelayCommand]
    private Task AllSolutions() => Target?.AllSolutionsCommand.ExecuteAsync(null) ?? Task.CompletedTask;
}