# app.console

The console is the **process boundary** — the OS entry that starts a plang
program. It reads which goal to run from the command line, builds the `app`
around that entry goal, and starts it.

```csharp
console.start(input)
  → new app(new goal { name = input ?? "start" }).start()
```

The console is the one thing *above* `app`: nothing in the program reaches it;
it reaches in to construct `app` and hand it the entry goal. The goal name comes
from the command line — with no argument, the program runs `start`.

## what it does

- reads `input` — the goal name from the command line. This is the **one place a
  raw string is allowed** (the perimeter); it is wrapped as `Data<text>`
  immediately, before it crosses into `app`.
- defaults to `"start"` when no goal is named.
- constructs the entry `goal` by name and the `app` that owns it.
- `start()`s the `app` and lets the returned `Data` decide the process result —
  `Ok` exits clean, an `Error` becomes a non-zero exit.

## execution

```
console.start(input)
 └─ new app(goal "start").start()
     └─ goal.start()
         └─ step.list.start() → step.start() → action.list.start() → action.start()
```

The console never sees a step or an action — it sees one `Data` come back out
the bottom of `app.start()`, and turns it into an exit code.

## OBP rules

- **The console is the perimeter.** A raw CLI string is allowed here and nowhere
  below — it becomes `Data<text>` before it crosses into `app`.
- **Console constructs `app`; `app` never reaches back.** Behavior flows down
  from here; the console only ever sees the final `Data`.
- **One default, named once.** No goal → `"start"`. The default lives here, at
  the boundary, not scattered through the runtime.

## source

`app/console/start.cs`
