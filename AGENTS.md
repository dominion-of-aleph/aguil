# aguil

Avalonia front end for AL. XAML-facing code is C# (`Aguil`); everything else is F#
(`Aguil.Core`, `Aguil.Editor`).

## Abstractions

Generic, composable components pay off. Follow the general conventions: generalize,
don't special-case. Express differences as data and compose existing controls before
adding another rendering path. Prefer Avalonia's components over custom layout machinery.

- Keep provenance separate from placement. Never infer an opening's source from coordinates
  or visual proximity. Source relationships guide initial placement; they do not constrain later
  moves or make closing one pane rearrange its descendants. Let Avalonia arrange the controls.
- State belongs to the scope it controls, with one authoritative owner. If an action affects a
  whole column, its controls must read and change that column's state; individual pane flags
  must not compete to determine the shared result.
- Every exposed mutation path must preserve the same invariants. Keep selection and lifecycle
  updates at the collection's owning boundary so ordinary add/remove operations and UI commands
  behave consistently. A convenience method must not be the only path that keeps state valid.

- `Aguil/Views` contains Avalonia UI hosts: `Repl`, `Inspector`, `DeferredContent`, `InlinePreview`, `Pane`,
  `PaneColumn`, and `PaneHost`.
  `Inspector` orders and hosts tabs; `InlinePreview` supplies the REPL's height cap and
  resize grip. Allocation of screen space belongs to the host.
- ALViews means object-provided inspection views in the GT/Phlow sense. AL discovers
  them through its own dispatch/inheritance and `to_map` supplies their descriptions.
  Keep that knowledge in AL rather than duplicating its class hierarchy in C#.
- `Aguil/Phlow` constructs these descriptions into `AlPhlowView` wrappers: a native
  `Control` plus title and priority. `PhlowBuilder` maps view names to implementations.
  `AlPhlowView` wraps any supplied control; `CSharpForward` exports native controls to AL.
  XAML and embedded inspectors use the same wrapper, without view-specific subclasses.
  AL's `brick_table` emits native `TableView` columns and recycling `DataTemplate`s.
- `Aguil/Editor` holds Avalonia editor integration;
  `Aguil.Editor` holds F# grammar, highlighting, and keymap logic.
- `Aguil.Core/AlValues.fs` holds AL values and printing. `Aguil.Core/MCP` isolates the
  transport and temporary evaluation adapter. MCP results become ordinary view descriptions
  before reaching the inspector. The eventual AL TCP/object API should replace this adapter.

Descriptions follow one path: descriptions -> `PhlowBuilder` -> `AlPhlowView` -> `Inspector`.
C#-authored controls enter the same host through `AlPhlowView` wrappers.
Raw participates through ordinary view discovery and priority (100). The MCP result's Raw
view configures a columned list with editor cells; bindings use embedded inspectors.
Keep the temporary MCP knowledge in its adapter, not in separate result-specific UI hosts.
AL text views use `phlow_text` -> `brick_editor` -> XAML. The host imports `ae` for
AvaloniaEdit and `editor` for the existing `Code` attached properties. Explicit slot
`xaml_name` metadata is a qualified property name; ordinary slots derive `Tag.Property`.
Reusable bricks and XAML serializers belong to AL's `views` package, not `window_manager`.
`Code.Text` binds two-way without resetting unchanged text (which would clear undo).
Raw displays use AL's `phlow_raw` and `brick_editor_template`; table columns select a
cell brick class, such as `brick_editor_cell` or `brick_text`. `phlow_clist` supplies
`brick_table` XAML and row data. `Bindings.Data` converts AL lists and row fields for
ordinary bindings; native templates preserve recycled controls. Row inspection targets
are inherited by cells. `Code.FollowCaret` handles outer scrolling.
The MCP adapter discovers view titles, priorities, and AL view values before rendering.
Raw uses the same deferred description path; binding inspectors discover their views when opened.
`DeferredContent` materializes selected tabs on load and retains their controls across tab switches.
Discovery runs AL's existing `view` methods; `to_map` and native construction wait until selection.
XAML slot metadata is resolved by name, with the nearest class overriding inherited specs.
Failed inspection requests leave displayed panes unchanged and report the error, without a fallback view.

`ObservationCache` shares objects by branch and object id; each object owns one slot per opened path.
One F# `Event` per client acts as the broker, broadcasting to filtered subscribers.
`CommandLogListener` reads each active branch independently and calls its publish callback.
Joining observers never rewind a shared reader: subscribe, then refresh the current object.
An object detects gaps from batch boundaries and recovers with a whole-object read.
`AlMutableObject` projects its commands; `AlMutableObjectSlot` exposes a bound property;
`AlObservableList` owns the observable item collection. Writes go to AL, not directly to other
controls. List deltas are an acceleration: a fresh snapshot must reconstruct the same contents.
Snapshot slot entries before notifying: a binding callback can open another slot on its owner.
Repeated identical `changes` assignments remain distinct within a batch; snapshot positions
prevent reapplying old deltas. Slot writes reread authoritative state, without acknowledgement cursors.
Cache references/data, never visual controls: each list view renders its own item controls.

The outer workspace observes AL's `wm` through `AlMutableObject.Watch`. AL's `layout_changed`
publishes `changes` together with the new layout, extent, and workspace XAML. Preserve every
assignment in order; snapshots restore membership without replaying the last event. Layouts
supply the workspace XAML through bricks in `views`. The native host retains unchanged content
controls, replaces changed content, and releases its subscription on close. `Bindings.Context`
carries the workspace branch to generated controls independently of inspection provenance.

`InspectionTarget` is an inheritable attached property on controls, not the inspector tool.
It carries the value to inspect. Individual list items/cells can have their own targets;
a view need not have one. Rebinding must replace or clear a stale target.
Keep text access independent of layout for future history search and selection across controls.

## Pane layout

`Pane` is an Avalonia `HeaderedContentControl` with a template for expand/close actions.
Its native `Header` and `Content` properties accept data or controls; the inspection target
supplies the header. `PaneHost` scrolls a horizontal strip of `PaneColumn` views; each column
scrolls its own Grid vertically. `PaneHost.Columns` is the strip's native child collection;
`PaneColumn.Panes` is the column Grid's native child collection. These are the owners.
`PaneHost.Panes` only enumerates them, and `Pane.Column` reads Avalonia's logical parent chain.
There is no second collection or stored owner to synchronize. Columns can be populated before
joining a host. Grid rows record placement within a column; `Aguil.Core/Layout.fs` finds a free row.

The initial allocation uses full-height rows, a full-width lone column, and half-width columns
otherwise. `PaneColumn` owns expansion; every pane header in that column binds to the same state.
Slots within a populated column survive individual closes. Closing a column's last pane
removes that column and shifts later columns left; the first workspace column stays available.
Remove or clear `PaneHost.Columns` to discard columns explicitly. Each collection's owning control handles its lifecycle;
the host observes membership changes to keep selection valid. Native validation rejects panes
in the host's column collection and non-pane controls in a column before insertion.

Layout policy will move to AL's scene and pane model, which supplies constraints to the UI.
Keep the native control composition separate from that policy and from inspection provenance.

`Aguil.Core/Inspection.fs` records a target and its source inspection independently of controls.
`ReplViewModel.Inspections` retains these openings for the session. Each REPL `Answer` owns its
inspection identity, independent of its index in the solutions collection. The inheritable
attached property `Inspector.Inspection` carries the source to targets.
Moving or closing panes does not erase those links. Alternative layouts and a provenance view
can later use that same history without deriving it from positions.

Inspection source links describe the exact value or answer: several inline inspections can share
one REPL pane. Pane placement does not need a second provenance tree.

- A REPL result hosts a small inline inspector. Evaluating input does not create a new column.
- Middle-clicking a target inserts a new inspector column immediately to the right of the REPL's
  first column. Older columns shift right, so the newest inspection is always beside the REPL.
  Opening restores the source and first column's normal width to make room for the new inspector.
- Pane actions sit at the top right; the REPL pane disables closing. One column fills the
  available space; additional columns share it. Thirds and other allocation policies can follow.
  View size hints remain requests subject to the host's allocation.
- Bring an opened or moved pane into view by scrolling its column vertically and the workspace
  horizontally. Other columns keep their vertical position, including the REPL's column.
  Descendant bring-into-view requests stop at the column; only host navigation reveals the column
  in the workspace, so an editor updating elsewhere cannot pull the workspace back.
- Closing B closes B alone. If its column becomes empty, later columns shift left to fill the gap.
  `Move` changes current placement without changing inspection history.
  Movement keybindings can follow.
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
