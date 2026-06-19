# app.goal.step

A step is one natural-language instruction inside a goal. The builder maps each step's `Text` to one or more `Actions` (module + action name + parameters). At runtime, `RunAsync` dispatches those actions in order.

## Key properties

| Property | Role |
|---|---|
| `Text` | The original natural-language instruction written by the developer |
| `Index` | Position within the goal's step list |
| `Indent` | Nesting depth — used by `if`/`foreach` to find sub-steps |
| `Actions` | The compiled action list produced by the builder |
| `Intent` | LLM's one-line summary of what the step does |

## Lifecycle

When a step runs, it fires `BeforeStep` events, then iterates its `Actions`. If any action sets `ShouldExit()` or `Handled`, the step stops early. `AfterStep` events fire regardless.

`Disabled` is set by condition actions to skip indented sub-steps without removing them.

## Source

[[PLang/app/goal/steps/step/this.cs]]
