# aguil

Avalonia front end for AL. XAML-facing code is C# (`Aguil`); everything else is F#
(`Aguil.Core`, `Aguil.Editor`).

## Abstractions

Generic, composable components pay off. Follow the general conventions: generalize,
don't special-case. Express differences as data and compose existing controls before
adding another rendering path. Prefer Avalonia's components over custom layout machinery.

- `Aguil/Views` contains Avalonia UI hosts: `Repl`, `Inspector`, and `InlinePreview`.
  `Inspector` orders and hosts tabs; `InlinePreview` supplies the REPL's height cap and
  resize grip. Allocation of screen space belongs to the host.
- ALViews means object-provided inspection views in the GT/Phlow sense. AL discovers
  them through its own dispatch/inheritance and `to_map` supplies their descriptions.
  Keep that knowledge in AL rather than duplicating its class hierarchy in C#.
- `Aguil/Phlow` constructs these descriptions into `AlPhlowView` wrappers: a native
  `Control` plus title and priority. `PhlowBuilder` maps view names to implementations.
  `CSharpForward` wraps a supplied control; `EmbeddedInspector` embeds another inspector.
  Neither implements forwarding to a particular view on another object.
  `ColumnedList` wraps Avalonia's `TableView` with any number of independently templated columns.
- `Aguil/Elements` builds controls without view metadata. A `Stencil` separates control
  creation from binding an item, allowing reuse. Cells contain ordinary controls.
- `Aguil/Editor` holds Avalonia editor integration and read-only editors;
  `Aguil.Editor` holds F# grammar, highlighting, and keymap logic.
- `Aguil.Core/AlValues.fs` holds AL values and printing. `Aguil.Core/MCP` isolates the
  transport and temporary evaluation adapter. MCP results become ordinary view descriptions
  before reaching the inspector. The eventual AL TCP/object API should replace this adapter.

Descriptions follow one path: descriptions -> `PhlowBuilder` -> `AlPhlowView` -> `Inspector`.
C#-authored controls enter the same host through `AlPhlowView` wrappers.
Raw participates through ordinary view discovery and priority (100). The MCP result's Raw
view configures a columned list with editor cells; bindings use embedded inspectors.
Keep the temporary MCP knowledge in its adapter, not in separate result-specific UI hosts.

`InspectionTarget` is an inheritable attached property on controls, not the inspector tool.
It carries the value to inspect. Individual list items/cells can have their own targets;
a view need not have one. Rebinding must replace or clear a stale target.
Keep text access independent of layout for future history search and selection across controls.

## Next layout work (planned)

The current REPL appends inspectors horizontally. The pane model below is the agreed direction,
not implemented behavior:

- A REPL result hosts a small inline inspector. Evaluating input does not create a new column.
- Middle-clicking a target opens an inspector pane immediately to the right of its source pane.
  If A already has B to its right, another inspection X from A goes below B in that column.
  Move B and its downstream branch upward together, preserving their visual provenance.
- Each pane has expand and close controls at the top right. One pane can fill the available
  space; additional panes share it. Halves, thirds, and full expansion are host allocation
  policies; view size hints are requests subject to that policy.
- Bring the new pane into view by scrolling. Closing B closes B alone for now.
- Keep the selected inspection/pane distinct from keyboard focus so commands can target it
  while the REPL input retains focus.

## Run it before you explain or change it

Build, then run the editor examples through their facts:

    dotnet build Aguil.slnx
    dotnet test Aguil.Editor.Tests/Aguil.Editor.Tests.fsproj --logger "console;verbosity=detailed"

The compiled functions in `Examples.fs` return data for interactive exploration;
`Facts.fs` prints their results and asserts on them.

- Check a claim by running an example, and show its real output. Never present made-up data as output.
- Keep examples small, composable, and useful for exploration; facts print their results and
  check stable behavioral contracts. No throwaway scripts.
- The inspector/view example suite was deliberately removed ahead of the layout redesign.
  Do not recreate broad UI scaffolding by default. Avoid assertions on incidental visual trees,
  container choices, or the exact views currently installed in a live AL image.
- Keep logic in plain functions the examples can call; keep agents and UI code thin around them.
- Follow `.claude/skills/general-conventions`; use `.claude/skills/git-conventions` for git.

## Housekeeping

- Format what you touch: `dotnet tool exec -y fantomas <files>` for F#, `dotnet format` for C#.
- Test: `dotnet test Aguil.slnx`.
- `Native/` holds upstream sources merged as git subtrees from release tags; update by merging the
  next tag. No submodules.
- Short names, one-line comments.
