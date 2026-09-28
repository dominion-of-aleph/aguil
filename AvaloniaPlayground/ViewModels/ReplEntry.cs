using AvaloniaPlayground.Core;

namespace AvaloniaPlayground;

public abstract record ReplEntry(string Source);

public sealed record ReplSuccess(string Source, AlEvaluation.EvaluationContext Result) : ReplEntry(Source);

public sealed record ReplFailure(string Source, string Error) : ReplEntry(Source);
