/// Keymaps: a key, as Avalonia writes it ("Alt+P"), to an action name. Plain data, so AL can send one.
module Aguil.Editor.Keymaps

open System
open System.Collections.Generic
open System.Reflection
open System.Windows.Input
open Avalonia.Input
open Avalonia.Interactivity
open AvaloniaEdit
open AvaloniaEdit.Editing

let defaults: IReadOnlyDictionary<string, string> =
    readOnlyDict [
        "Ctrl+P", "MoveUpByLine"
        "Ctrl+N", "MoveDownByLine"
        "Ctrl+F", "MoveRightByCharacter"
        "Ctrl+B", "MoveLeftByCharacter"
        "Ctrl+A", "MoveToLineStart"
        "Ctrl+E", "MoveToLineEnd"
        "Alt+F", "MoveRightByWord"
        "Alt+B", "MoveLeftByWord"
        "Ctrl+D", "Delete"
        "Alt+D", "DeleteNextWord"
        "Ctrl+Space", "ToggleMark"
        "Ctrl+G", "Cancel"
        "Alt+P", "PreviousInput"
        "Alt+N", "NextInput"
    ]

/// AvaloniaEdit's own commands by name.
let editorCommands =
    [
        typeof<EditingCommands>
        typeof<ApplicationCommands>
        typeof<AvaloniaEditCommands>
    ]
    |> Seq.collect (fun t -> t.GetProperties(BindingFlags.Public ||| BindingFlags.Static))
    |> Seq.map (fun property -> property.GetValue null :?> RoutedCommand)
    |> Seq.distinctBy _.Name
    |> Seq.map (fun command -> command.Name, command)
    |> readOnlyDict

/// Runs an AvaloniaEdit command, else the data context's command of that name (PreviousInput runs
/// PreviousInputCommand). False when there's neither.
let private run (area: TextArea) (action: string) =
    match editorCommands.TryGetValue action with
    | true, command ->
        command.Execute(null, area)
        true
    | _ ->
        match area.DataContext with
        | null -> false
        | context ->
            match context.GetType().GetProperty(action + "Command") with
            | null -> false
            | property ->
                match property.GetValue context with
                | :? ICommand as command ->
                    if command.CanExecute null then
                        command.Execute null

                    true
                | _ -> false

let private modifierKeys =
    set [
        Key.LeftCtrl
        Key.RightCtrl
        Key.LeftShift
        Key.RightShift
        Key.LeftAlt
        Key.RightAlt
        Key.LWin
        Key.RWin
    ]

/// Handles the keymap's keys before AvaloniaEdit does, so they can replace its own.
let install (area: TextArea) (keymap: IReadOnlyDictionary<string, string>) =
    let bindings = [ for KeyValue(key, action) in keymap -> KeyGesture.Parse key, action ]
    let mutable marking = false

    area.AddDisposableHandler(
        InputElement.KeyDownEvent,
        EventHandler<KeyEventArgs>(fun _ e ->
            match bindings |> List.tryFind (fun (gesture, _) -> gesture.Matches e) with
            | Some(_, "ToggleMark") ->
                marking <- not marking
                area.ClearSelection()
                e.Handled <- true
            | Some(_, "Cancel") ->
                marking <- false
                area.ClearSelection()
                run area "Cancel" |> ignore
                e.Handled <- true
            | Some(_, action) when marking && action.StartsWith "Move" ->
                e.Handled <- run area ("Select" + action.Substring "Move".Length)
            | Some(_, action) ->
                marking <- false
                e.Handled <- run area action
            | None when not (modifierKeys.Contains e.Key) -> marking <- false
            | None -> ()),
        RoutingStrategies.Tunnel
    )
