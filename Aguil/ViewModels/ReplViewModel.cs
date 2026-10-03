using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Aguil.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aguil.ViewModels;

public partial class ReplViewModel : ObservableObject
{
    private readonly AlMcpClient _al = new();

    public ObservableCollection<ReplEntry> History { get; } = [];

    [ObservableProperty] public partial string Source { get; set; } = "";

    // what PreviousInput/NextInput walk, oldest first: earlier sessions' inputs from AL, then this one's
    public List<string> Inputs { get; } = [];

    public async Task LoadPreviousInputs()
    {
        try
        {
            Inputs.InsertRange(0, await _al.PreviousInputs());
        }
        catch (Exception e) when (e is AlException or HttpRequestException)
        {
            // AL isn't running: start without earlier inputs
        }
    }

    // which input PreviousInput/NextInput show (null: a new one), and the new one put aside
    private int? _recall;
    private string _draft = "";

    [RelayCommand]
    private void PreviousInput()
    {
        if (Inputs.Count == 0) return;
        if (_recall is null) _draft = Source;
        _recall = Math.Max((_recall ?? Inputs.Count) - 1, 0);
        Source = Inputs[_recall.Value];
    }

    [RelayCommand]
    private void NextInput()
    {
        if (_recall is null) return;
        _recall = _recall + 1 < Inputs.Count ? _recall + 1 : null;
        Source = _recall is { } index ? Inputs[index] : _draft;
    }

    [RelayCommand]
    private async Task Run()
    {
        _recall = null;
        var source = Source;
        Inputs.Add(source);
        Source = "";
        ReplEntry entry;
        // Replace with a real sum type logic
        try
        {
            var (result, views) = await _al.QueryAlViews(source, null);
            entry = new ReplSuccess(source, new AlEvaluation.Solution(result, views), _al);
        }
        catch (AlException e)
        {
            entry = new ReplFailure(source, e.Message);
        }

        History.Add(entry);
        Selected = entry;
    }

    [ObservableProperty] public partial ReplEntry? Selected { get; set; }

    public ReplSuccess? Target =>
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
