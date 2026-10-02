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
├─ /system/shortcut/*.goal                             — system shortcuts (the app's own and the os's, by name);
│   └─ app.goal.list.Find("/system/shortcut/<name>")     the app's copy first; their names are sealed
└─ /shortcut/*.goal → Find("/shortcut/<name>")         — the app's; a sealed name is refused (ShortcutCollision)
app.Launch                                             reads the list before anything runs: a clash stops the start
```

A shortcut is system when its goal is (`IsSystem => Goal.IsSystem`: a goal under `system/`). The one way to change a
system shortcut is the app's own `/system/shortcut/<name>.goal` — it overrides the os's, as any app `/system/` goal
does; an app `/shortcut/<name>.goal` taking a system name is refused, and the refusal says so. A shortcut goal that
isn't built is left out, said on the debug channel.

A shortcut goal reads its asker through the stack: inside it, `%!app.callstack.scope%` is its own goal's frame
and `.caller` the asker's action frame — `return %!app.callstack.scope.caller.goal%`. A shortcut that reads its
own name recurses until the call-stack depth guard stops it (an error, not a hang).

## Tests

- `PLang.Tests/Runtime/App/Shortcut/ShortcutTests.cs` — a shortcut reads as its goal's answer for its asker; a read
  leaves the asker's `%!data%` as it was; a shortcut answers before the app's member and `%!app.x%` still reaches
  the member; the list holds the app's shortcuts; the app's own `/system/shortcut/` reads as the shortcut and is
  sealed; an app shortcut taking a system name is refused, naming `/system/shortcut/<name>.goal`.

## Known faults

- The list is read once: a shortcut goal built mid-run is not one until the app starts again.
- A read costs one goal call (~0.2 ms, against ~0.03 ms for a value held in memory).
