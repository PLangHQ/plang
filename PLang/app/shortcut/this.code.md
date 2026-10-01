# shortcut: a name that reads as a goal's answer

`%!x%` is the app's member `x` unless a shortcut named `x` exists; then the shortcut wins. A shortcut is a goal
under a `shortcut/` folder, named by its file. So `%!goal%` (the goal in play, `/system/shortcut/goal.goal`)
differs from `%!app.goal%` (the concept node): the `app.` path always reaches the member.

## Entry points

```
%!x% read                                              type/item/variable/code/Variable.cs  Start
├─ own memory (context.Variable.Get)                   — !app, !context, !data … answer first
├─ app.shortcut.Get(x) → shortcut.Read(asker)          shortcut/this.cs  Read
│   └─ await using asker.Variable.Calls.Isolate(null)  — every write the goal makes ends with the read
│      goal.Start(asker)                               — its return settles in its own frame
├─ the app's member x (!app.Get)
└─ the module named x
app.shortcut                                           app/this.cs  (type.@this<shortcut, shortcut.list>)
shortcut.list.Read()                                   shortcut/list/this.cs — once (Lazy), at the first ask
├─ <os>/system/shortcut/.build/*.pr                    — system shortcuts; their names are sealed
└─ /shortcut/.build/*.pr                               — the app's; a sealed name is refused (ShortcutCollision)
app.Launch                                             reads the list before anything runs: a clash stops the start
```

A shortcut goal reads its asker through the stack: inside it, `%!app.callstack.scope%` is its own goal's frame
and `.caller` the asker's action frame — `return %!app.callstack.scope.caller.goal%`. A shortcut that reads its
own name recurses until the call-stack depth guard stops it (an error, not a hang).

## Tests

- `PLang.Tests/Runtime/App/Shortcut/ShortcutTests.cs` — a shortcut reads as its goal's answer for its asker; a read
  leaves the asker's `%!data%` as it was; a shortcut answers before the app's member and `%!app.x%` still reaches
  the member; the list holds the app's shortcuts.

## Known faults

- The list is read once: a shortcut goal built mid-run is not one until the app starts again.
- A read costs one goal call (~0.2 ms, against ~0.03 ms for a value held in memory).
