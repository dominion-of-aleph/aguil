using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
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
    private readonly AlMcpClient _al;

    public ReplSuccess(string source, Evaluation.Solution first, AlMcpClient al)
        : base(source)
    {
        _al = al;
        Solutions.Add(first);
    }

    public ObservableCollection<Evaluation.Solution> Solutions { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Solution))]
    [NotifyPropertyChangedFor(nameof(Views))]
    [NotifyPropertyChangedFor(nameof(Result))]
    [NotifyPropertyChangedFor(nameof(Position))]
    [NotifyCanExecuteChangedFor(nameof(PrevCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    public partial int Index { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    public partial bool Exhausted { get; set; }

    public Evaluation.Solution Solution => Solutions[Index];

    public IEnumerable<FSharpMap<Value, Value>> Views => Core.MCP.Views.fromSolution(Solution);

    public Evaluation.EvaluationContext Result => Solution.Result;

    public Evaluation.EvaluationContext Frontier => Solutions[^1].Result;

    public string Position => $"{Index + 1}/{Solutions.Count}";

    private bool CanPrev() => Index > 0;

    private bool CanNext() =>
        Index < Solutions.Count - 1 || (Frontier.HasPotentialSolution && !Exhausted);

    [RelayCommand(CanExecute = nameof(CanPrev))]
    private void Prev() => Index--;

    [RelayCommand(CanExecute = nameof(CanNext))]
    private async Task Next()
    {
        if (Index < Solutions.Count - 1)
        {
            Index++;
            return;
        }

        // hasPotentialSolution does not promise an answer, exhaustion comes back as a failure
        Evaluation.EvaluationContext result;
        try
        {
            result = await _al.NextSolution(Frontier.Context);
        }
        catch (AlException)
        {
            Exhausted = true;
            return;
        }

        var views = await _al.AlViewsFromQuery(result, null);
        Solutions.Add(new Evaluation.Solution(result, views));
        Index = Solutions.Count - 1;
    }

    // the search can be unbounded, so stop and leave Next enabled rather than being Hina
    private const int Batch = 100;

    [RelayCommand]
    private async Task AllSolutions()
    {
        for (var i = 0; i < Batch && NextCommand.CanExecute(null); i++)
            await NextCommand.ExecuteAsync(null);
    }
}
