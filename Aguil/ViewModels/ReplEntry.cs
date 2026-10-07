using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Aguil.Core;
using Aguil.Core.MCP;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.FSharp.Collections;
using Value = Aguil.Core.AlValues.AlValue;

namespace Aguil.ViewModels;

public abstract class ReplEntry(string source)
{
    public string Source { get; } = source;
}

public sealed class ReplFailure(string source, string error) : ReplEntry(source)
{
    public string Error { get; } = error;
}

[ObservableObject]
public sealed partial class ReplSuccess : ReplEntry
{
    // the search can be unbounded, so stop and leave Next enabled rather than being Hina
    private const int Batch = 100;
    private readonly AlMcpClient _al;

    public ReplSuccess(string source, Answer first, AlMcpClient al)
        : base(source)
    {
        _al = al;
        Solutions.Add(first);
        Solutions.CollectionChanged += (_, _) =>
        {
            var index = Math.Clamp(Index, 0, Math.Max(0, Solutions.Count - 1));
            if (index != Index) Index = index;
            else Refresh();
        };
    }

    public ObservableCollection<Answer> Solutions { get; } = [];

    [ObservableProperty] public partial int Index { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    public partial bool Exhausted { get; set; }

    public Evaluation.Solution? Solution => Solutions.ElementAtOrDefault(Index)?.Solution;

    public Inspection? Inspection => Solutions.ElementAtOrDefault(Index);

    public IEnumerable<FSharpMap<Value, Value>> Views =>
        Solution is { } solution ? Core.MCP.Views.fromSolution(solution) : [];

    public Evaluation.EvaluationContext? Result => Solution?.Result;

    public Evaluation.EvaluationContext? Frontier => Solutions.LastOrDefault()?.Solution.Result;

    public string Position => $"{(Solution is null ? 0 : Index + 1)}/{Solutions.Count}";

    partial void OnIndexChanged(int value)
    {
        Refresh();
    }

    private bool CanPrev()
    {
        return Solution is not null && Index > 0;
    }

    private bool CanNext()
    {
        return Index < Solutions.Count - 1 || (Frontier is { HasPotentialSolution: true } && !Exhausted);
    }

    private void Refresh()
    {
        OnPropertyChanged(nameof(Solution));
        OnPropertyChanged(nameof(Views));
        OnPropertyChanged(nameof(Inspection));
        OnPropertyChanged(nameof(Result));
        OnPropertyChanged(nameof(Position));
        PrevCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanPrev))]
    private void Prev()
    {
        Index--;
    }

    [RelayCommand(CanExecute = nameof(CanNext))]
    private async Task Next()
    {
        if (Index < Solutions.Count - 1)
        {
            Index++;
            return;
        }

        if (Frontier is not { } frontier) return;

        // hasPotentialSolution does not promise an answer, exhaustion comes back as a failure
        Evaluation.EvaluationContext result;
        try
        {
            result = await _al.NextSolution(frontier.Context);
        }
        catch (AlException)
        {
            Exhausted = true;
            return;
        }

        var views = await _al.AlViewsFromQuery(result, null);
        Solutions.Add(new Answer(new Evaluation.Solution(result, views)));
        Index = Solutions.Count - 1;
    }

    [RelayCommand]
    private async Task AllSolutions()
    {
        for (var i = 0; i < Batch && NextCommand.CanExecute(null); i++)
            await NextCommand.ExecuteAsync(null);
    }
}