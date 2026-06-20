# app.goal

A goal is a named sequence of steps. It is the basic unit of a plang program.

```csharp
goal(Data<text> name, Data<path> path, step.list step)
```

A goal runs by starting its steps. What does `goal.start()` want to do? Run all
the steps — run the list:

```csharp
public Task<data.@this> start() => step.list.start();
```

The steps ride as `Data<step.list>` — a collection is a value, so it flows
wrapped like everything else. Taking a bare `step.list` would force the caller
to crack open a Data to hand it over; that is the decomposition OBP forbids. The
goal navigates to the list and starts it — it never loops, and never opens
`Data.Value` (only a leaf does that).

## execution

```
goal.start()
 └─ step.list.start()         the list owns the loop, runs steps in order
     └─ step.start()
         └─ action.list.start()
             └─ action.start()    e.g. new db(Data<Db> db).start()
```

## structure

Generated from the source at build time — never hand-maintained:

[[app/goal/start.json]]

There is no `goal.steps`. The concept is singular (`step`); the collection of
them is `step.list`. Same shape as the root: `app.goal.list`, `app.goal["Start"]`.

## returns

A method returns one of two shapes, chosen by the signature:

- `Task<data.@this<T>>` — a value of a known type. A db read returns
  `Data<table>`, `math.add` returns `Data<number>`. **Prefer this** whenever the
  type is known.
- `Task<data.@this>` (bare) — the value is polymorphic or forwarded.

`goal.start()` returns the **bare** form: it forwards whatever the last step
produced, and that type isn't known until runtime — so it stays
`Task<data.@this>`. `Ok` when all steps complete, the first `Error` otherwise.
The error short-circuits: remaining steps do not run, and the goal returns it
as-is without inspecting it.

## OBP rules

- **A goal owns its steps.** `step.list` is owned here, not shared between goals.
- **Run = start the list.** `goal.start()` delegates to `step.list.start()`. No loop.
- **No plurals.** `goal.step.list`, never `goal.steps`.
- **Behavior flows down.** A step never reaches back up to its goal.
- **Error short-circuits.** The list stops at the first failed step and returns the error.

## source

`app/goal/start.cs`
