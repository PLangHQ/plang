# coder — event-action — v3: stage 5 (system events, lazy load)

Rulings 53–59.

1. `event.setting.system: load | skip` (default load); `os/system/event/Hello.goal` (writes out "This is system event" before the app starts).
2. The actor's events load on their first ask or add (`On(item)`, `On(name)`, `Add`, `Add(name, event)`): the System actor's first (every goal is read as System), then the system/event files run as this actor (the root: its own then the os's; a sub app: its own only), then what the maker's actor of its role bound, each taken in (a sub app's own file of a name replaces the maker's from that file). An unbuilt file is one line on the error channel; the next one loads. The loading flow goes through (AsyncLocal).
3. One door each: `On(item)` answers at once when loaded (no `Held`, 56); `On(name)` answers the list a set runs, the name's own or the variable type's, never null (57/59); `type.Create` keeps its signature, awaiting only on a first ask (56).
4. The snapshot's read loads the actors' events first, then binds what it kept (system, inherited, saved; a record that doesn't read refuses the restore).
5. Callers moved to the async door; `app.event(context)` async (54); `Scope.cs` deleted (start.md:72).
6. Pins (C#): SystemEventsTests (5), the variable door's one answer, a birth after the load answering at once.
7. Two goal comments reworded (EventAfterAction, AfterEachLlmQuery); SystemGoalsLeftAlone read.
