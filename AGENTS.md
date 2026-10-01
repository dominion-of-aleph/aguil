# aguil

Avalonia front end for AL. XAML-facing code is C# (`Aguil`); everything else is F#
(`Aguil.Core`, `Aguil.Editor`).

## Run it before you explain or change it

Build, then explore the editor in F# Interactive:

    dotnet build Aguil.slnx
    dotnet fsi --use:Aguil.Editor.Tests/Examples.fsx

It prints every example, then leaves a prompt: `show (docHeredoc ());;`, `Styler.style ...`.

- Check a claim by running an example, and show its real output. Never present made-up data as output.
- New behaviour gets a composable example in `Examples.fs` (returns data, builds on earlier ones)
  and a fact in `Facts.fs` (prints the example, then asserts). No throwaway scripts.
- Keep logic in plain functions the examples can call; keep agents and UI code thin around them.
- Follow `.claude/skills/general-conventions`; use `.claude/skills/git-conventions` for git.

## Housekeeping

- Format what you touch: `dotnet tool exec -y fantomas <files>` for F#, `dotnet format` for C#.
- Test: `dotnet test Aguil.slnx`.
- `Native/` holds upstream sources merged as git subtrees from release tags; update by merging the
  next tag. No submodules.
- Short names, one-line comments.
