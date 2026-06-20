# app.goal.step

A step is **one instruction** — one line of a goal as the developer wrote it,
plus the action(s) it compiled to. A goal is a sequence of steps; a step is the
unit that runs.

```csharp
step(Data<text> text, Data<number> index, Data<action.list> action)
```

- `text` — the instruction as written (`- write out %name%`). The
  natural-language source line; what the developer sees and edits.
- `index` — its position in the goal, from `0`.
- `action` — the action(s) this step compiled to, as `Data<action.list>`.

A step runs by starting its actions. What does `step.start()` want to do? Run the
actions — run the list:

```csharp
public Task<data.@this> start() => action.start();   // Data<action.list> forwards start
```

The step never loops and never opens `Data.Value`; it forwards to the
`action.list`, which owns the loop.

## execution

```
step.start()
 └─ action.list.start()      the list owns the loop, runs actions in order
     └─ action.start()       the leaf — does the work, returns its value
```

A step's value is its **last action's** value: `action.list` forwards the last
result up, so `write out %name%` carries the written value back through the step.
On the first failed action the list stops and returns that error as-is.

## one step, usually one action

Most steps compile to a single action — v0.1 can't combine two modules in one
step. The collection is still `action.list`; a step that does map to more than
one action needs no new shape.

## structure

Generated from the source at build time — never hand-maintained:

[[app/goal/step/start.json]]

There is no `step.actions`. The concept is singular (`action`); the collection is
`action.list`. Same shape as `goal.step.list`, `app.goal.list`.

## OBP rules

- **A step owns its actions.** `action.list` is owned here, run by starting it.
- **Run = start the list.** No loop in the step; `action.list` owns it.
- **No plurals.** `step.action.list`, never `step.actions`.
- **The step forwards, never opens.** `step.start()` returns the `action.list`
  result as-is — it never reads `Data.Value`.
- **Error short-circuits.** The list stops at the first failed action.

## source

`app/goal/step/start.cs`
