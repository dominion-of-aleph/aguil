using Aguil.Core;
using Aguil.Core.MCP;

namespace Aguil.ViewModels;

public sealed class Answer(Evaluation.Solution solution) : Inspection(solution, null, solution.Result.Branch)
{
    public Evaluation.Solution Solution => (Evaluation.Solution)Target;
}