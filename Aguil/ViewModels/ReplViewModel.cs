using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Aguil.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aguil.ViewModels;

public partial class ReplViewModel : ViewModelBase
{
    private readonly AlMcpClient _al = new();

    public ObservableCollection<ReplEntry> History { get; } = [];

    [ObservableProperty] public partial string Source { get; set; } = "";

    // which earlier input PreviousInput/NextInput show (null: a new one), and the new one put aside
    private int? _recall;
    private string _draft = "";

    [RelayCommand]
    private void PreviousInput()
    {
        if (History.Count == 0) return;
        if (_recall is null) _draft = Source;
        _recall = Math.Max((_recall ?? History.Count) - 1, 0);
        Source = History[_recall.Value].Source;
    }

    [RelayCommand]
    private void NextInput()
    {
        if (_recall is null) return;
        _recall = _recall + 1 < History.Count ? _recall + 1 : null;
        Source = _recall is { } index ? History[index].Source : _draft;
    }

    [RelayCommand]
    private async Task Run()
    {
        _recall = null;
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