# Coder summary — branch `goal-graph-singular`

## Where things stand (2026-09-23)

The live work list is `open-items.md` (open first, done at the bottom with commits). This summary covers the
most recent work; older landed work is listed in open-items' Done section.

## What this is

The branch makes the goal graph singular and OBP-clean. The latest stretch:

- the list model (only a chunk dissolves);
- error-handling visibility;
- goal tags as the goal's own fact;
- the `clr`-named wrapper types;
- the actor as a choice;
- the births pass: making the shared program safe for concurrent actors.

## What was done (latest first)

- **Births step 1 — `ee03f91f2`.** The program graph is shared by every run.
  - A run never writes it: `action[name]` selects the row, and the generator gives each run its own Data
    (`Copy(context)` / `As<T>(context)`) born with the run's context.
  - A value loads with the context of the Data that asks (`source`/`wire`), never its own stored one.
  - `goal.call` binds each argument as a caller-born copy. Behaviour fix: arguments from a real .pr never bound.
  - A stored Data keeps its birth context: the Variables stamp and walk are gone.
  - The generated channel lookup reads the run's copy.
  - Tests: `PLang.Tests/Runtime/App/Goals/SharedProgramTests.cs` (8, rows from a real .pr).
- **Births step A — `26823c1d1`.** The running context is an instance AsyncLocal on the App
  (`app.Context`), set by `action.Run`/`goal.Run`/`Start()`. Kept for now; the type-object step decides
  whether it stays.
- **Births step B — dropped** (Ingi): values reading the running context was the wrong scope. It's parked
  in a git stash for reference. `proto-running-context.md` and `births-B-blocker.md` record why.
- **#20 actor as a choice — `1bf67dff4`.** `choice<actor>` over `{system, user}` on 5 slots, and `app.Actor[name]`.
- **#19 — `6031fdfd7`, `23d9259fa`.** List actions return the native list; setting.set/remove return no value.
  `[Masked]` is covered by a test-only item.
- **#8 tags — `22e6dafd6`.** The goal owns its tags; `test.Create`; discover collapsed.
- **#0b — `10829ad4f`.** List chunk rule `143b5ad9d`.

## Next

- Births step 3 (the type object from the registry, which closes #27) is **on hold**. The architect is
  taking the design to Ingi and will send the plan. After it: #15 and #24.
- Open items: see `open-items.md` (#28 same-actor list.add race, #29 file:// not stripped, …).

## Code example

The generator's per-run binding. The row is never written; each run gets its own Data:

```csharp
private static global::app.data.@this __Copy(action, string name, context)
    => action?[name]?.Copy(context) ?? global::app.data.@this.NotFound(name);   // plain slot
private static global::app.data.@this<T> __View<T>(action, string name, context)
    => action?[name]?.As<T>(context) ?? global::app.data.@this.NotFound(name).As<T>();   // typed slot
```

and a value loads with whoever asks:

```csharp
var asking = data.Context;                 // source.Value(data)
var resolved = await Get(asking);          // %ref% resolves in the asking run's variables
```
