# app.goal

A goal is a named sequence of steps — the basic unit of a plang program. It is
born one of two ways, and either way it ends up holding the **located** compiled
file:

- **by `name`** — a reference (`new goal { name = "start" }`, or a `call
  SomeGoal`). The goal finds the matching `.pr` and keeps its path as `prPath`.
- **by `prPath`** — already located (the loader hands over a `Path` to a `.pr`).

```csharp
goal(Data<text> name?, Data<path> prPath?) : IStart
```

Exactly one is given. The point of resolving to `prPath` at birth is that
`start()` becomes trivial: the file is already found, so starting is just a
read.

```csharp
public Task<data.@this> start() => read().start();   // prPath → step.list → start
```

`read()` loads the `.pr` at `prPath` into the `step.list`, then the goal starts
the list. The read is **deferred to `start()`** — finding the file (resolving
`name → prPath`) is cheap path work done at birth; opening it is I/O, and I/O
never happens in a constructor (OBP smell #9).

## execution

```
goal.start()
 └─ read()                    prPath → step.list   (load the compiled .pr)
     └─ step.list.start()     the list owns the loop, runs steps in order
         └─ step.start()
             └─ action.list.start()
                 └─ action.start()    e.g. new db(Data<Db> db).start()
```

`goal.start()` returns the **bare** `Task<data.@this>`: it forwards whatever the
last step produced, and that type isn't known until runtime. `Ok` when all steps
complete, the first `Error` otherwise. The error short-circuits — remaining steps
do not run, and the goal returns it as-is without inspecting it.

## structure

Generated from the source at build time — never hand-maintained:

[[app/goal/start.json]]

There is no `goal.steps`. The concept is singular (`step`); the collection is
`step.list` — and it isn't a constructor field, it is read from `prPath` at
`start()`. Same naming shape as the root: `app.goal.list`, `app.goal["Start"]`.

## name → prPath

Resolving a `name` to its `.pr` is the one open mechanism here. It is **not** a
file read — it is locating the file. Two candidates, to settle separately:

- **registry** — `app.goal.list` already knows every goal's `prPath` from the
  build; the goal asks the registry by name.
- **convention** — the `.pr` lives at a path derived from the name
  (`.build/{name}/…`), computed without touching disk.

Either way it stays cheap and synchronous; the disk read waits for `start()`.

## OBP rules

- **A goal holds its located file, not its steps.** `prPath` is the handle;
  `step.list` is read from it at `start()`, owned by the goal once read.
- **Find at birth, read at start.** Resolving `name → prPath` is path work; the
  `.pr` read is deferred — never in the constructor.
- **Run = read then start the list.** No loop in the goal; `step.list` owns it.
- **No plurals.** `goal.step.list`, never `goal.steps`.
- **Behavior flows down.** A step never reaches back up to its goal.
- **Error short-circuits.** The list stops at the first failed step and returns the error.

## source

`app/goal/start.cs`
