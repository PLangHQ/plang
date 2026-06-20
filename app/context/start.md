# app.context

Context is the **request-level state of a single execution** — the things that
are true *for this run*, not for the program.

`app` is the program. `context` is one run of it.

```csharp
context(Data<text> id, app app, variable.list variable)
```

## what it holds

| member | what it is |
|--------|-----------|
| `id` | unique identifier for this execution |
| `app` | back-reference to the root — reach anything from here |
| `variable` | the `%variables%` set during this run |
| `goal` | the goal currently executing (`app.goal.current`) |
| `step` | the step currently executing (`goal.step.current`) |

Context is **per-run**, `app` is **per-program**. The split is OBP rule 6:
per-request state is navigated through `context`; per-program state lives on `app`.

## you never pass context

Context is not a method parameter. Threading it through every signature is the
friction OBP exists to remove. A class that needs context declares `IContext`:

```csharp
class read : IContext {
    public context Context { get; set; }   // injected by the runtime

    public Task<data.@this> Run() {
        var path = Context.variable.Get("path");
        ...
    }
}
```

- The runtime sets `Context`. The class never reaches for it, never stores it on
  a shared object, never passes it on.
- Only **leaf actions** that actually touch run state implement `IContext`.
- A `goal`, a `step`, a `step.list` never see context — they just `start()`.

## why per-run, never stored on shared objects

A `goal` or `step` is shared across threads and runs. Storing context on it
would let one run's state leak into another. Context stays separate, born fresh
per execution, reached only by the leaves that need it — through `IContext`,
never through a field on the shared object.

## OBP rules

- **Context is navigated, not passed.** Implement `IContext`; the runtime injects it.
- **`app` is the program, `context` is the run.** Per-program state on `app`,
  per-run state on `context`.
- **Only leaves implement `IContext`.** A courier that asks for context is
  usually a leaf pretending to be a relay.
- **Never store context on a shared object.** It is per-run; the shared object is not.

## source

`app/context/start.cs`
