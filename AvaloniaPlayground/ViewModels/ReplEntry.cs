using System.Collections.ObjectModel;
using System.Threading.Tasks;
using AvaloniaPlayground.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaPlayground.ViewModels;

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

    public ReplSuccess(string source, AlEvaluation.EvaluationContext first, AlMcpClient al)
        : base(source)
    {
        _al = al;
        Solutions.Add(first);
    }

    public ObservableCollection<AlEvaluation.EvaluationContext> Solutions { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Result))]
    [NotifyPropertyChangedFor(nameof(Position))]
    [NotifyCanExecuteChangedFor(nameof(PrevCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    public partial int Index { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    public partial bool Exhausted { get; set; }

    public AlEvaluation.EvaluationContext Result => Solutions[Index];

    public AlEvaluation.EvaluationContext Frontier => Solutions[^1];

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
        try
        {
            Solutions.Add(await _al.NextSolution(Frontier.Context));
            Index = Solutions.Count - 1;
        }
        catch (AlException)
        {
            Exhausted = true;
        }
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