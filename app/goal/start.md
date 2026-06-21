# app.goal

A goal is a named sequence of steps. It is the basic unit of a PLang program.

```csharp
goal(Data<text> name, Data<path> path, step.list steps)
  → steps.start()
```

A goal runs by starting its step list. Steps run in order. A goal does not know
what its steps do — it owns them and starts them.

## execution

```
goal.start()
 └─ step.list.start()
     └─ step.start()          for each step in order
         └─ action.list.start()
             └─ action.start()    e.g. new db(Data<Db> db).start()
```

## signature

| parameter | type | meaning |
|-----------|------|---------|
| `name` | `Data<text>` | the goal's identifier — matches the `.goal` filename |
| `path` | `Data<path>` | where the `.goal` file lives on disk |
| `steps` | `step.list` | the ordered steps, owned by this goal |

## returns

`Task<data.@this>` — `Ok` when all steps complete, the first `Error` otherwise.
The error short-circuits: remaining steps do not run.

## OBP rules

- **A goal owns its steps.** Steps are not shared between goals.
- **Behavior flows down.** A step never reaches back up to its goal.
- **The list owns the loop.** `step.list` iterates; `step` does one thing.
- **Error short-circuits.** The list stops at the first failed step and returns
  the error upstream. The goal does not inspect the error — it returns it as-is.

## source

`app/goal/start.cs`
