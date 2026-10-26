# How a step runs

For someone changing the runtime. The question this page answers: what happens between `plang` and the C# that does a step's work?

## The chain

A goal holds steps, a step holds actions, an action is one call of a module's action. Each level has a `Start` that wraps the level below it in a frame and in the events bound to it.

1. **Goal** (`PLang/app/goal/this.cs:400`). `Start(context, parameters)` refuses a goal with a broken step (`GoalBroken`, `:407`, `:430-438`), pushes a call frame (`:415`), runs what is bound before it, then `Enter` (`:441`), then what is bound after it. `Enter` starts the steps (`:444`).
2. **Step** (`PLang/app/goal/step/this.cs:130`). Pushes its frame (`:136`), runs what is bound before, then `Code.Start(context)` (`:150`): the step's actions, as a chain. Then what is bound after.
3. **Action** (`PLang/app/goal/step/action/this.cs:177`). Pushes its frame (`:187`), `Attempt`s (`:196`), and on a failure lets the first `on.error` clause bound on it answer (`:201`). On success it stores the result as `%!data%` (`:205`). An `Attempt` (`:215`) runs what is bound before, then `DispatchAsync`, then what is bound after.
4. **Dispatch** (`:303`). `Instance` (`:352`) finds the action in the module and makes a shell. `Resolve` (`:318`) builds a fresh, fully populated handler from the `.pr` parameters. Then `real.Start()` (`:330`) runs the handler.

An error that travels as an exception is caught at the action (`:333`) and at the step (`step/this.cs:153`) and becomes the level's answer, whole. Any other exception becomes `Error.FromException` (`:336`, `step/this.cs:156`). One exception type is left out of the action's catch: `NullReferenceException` (`:334`). It goes up to the step, whose catch (`step/this.cs:154`) takes everything but out-of-memory, stack overflow and cancellation. See [the rules](rules.md).

## The handler

A handler is a `partial` class marked `[Action("name")]`. `PLang/app/module/math/add.cs` is a small one:

```csharp
[Action("add")]
public partial class Add : IContext
{
    public partial data.@this A { get; init; }
    public partial data.@this B { get; init; }

    [Default(number.Overflow.Promote)]
    public partial data.@this<global::app.type.item.choice.@this<number.Overflow>> Overflow { get; init; }

    [Code]
    public partial global::app.module.math.code.IMath Math { get; }

    public async Task<data.@this<number>> Start() => await Math.Add(this);
}
```

- **Parameters are `partial` properties** of type `data.@this`, `data.@this<T>` or marked `[Code]` (`add.cs:8-20`). The source generator (`PLang.Generators`) writes the other half of the class. A property of any other kind fails the build with `PLNG001`, an error: "Property '{0}' on action '{1}' must be Data<T> or [Code] T. Raw scalars are not permitted." (`PLang.Generators/Discovery/this.cs:43-49`).
- **`Start()` is the handler's own entry** (`add.cs:22`). The generator also writes the dispatcher, `ICodeGenerated.Start()` (`PLang/app/module/ICodeGenerated.cs`), as an explicit interface member, so it shares the name in one class. Start an action through the action's `Start(context)`, never the handler directly.
- **`ICodeGenerated`** has `Resolve` (build a populated handler from the `.pr`), `Attach` (set the context and markers) and `Start`. Handlers never implement it themselves: the generator adds it (`ICodeGenerated.cs`: "Generated handlers implement it explicitly, beside their own Start()").

## What `Start()` returns

Every `Start()` returns a `Data`. The type in the signature is the action's declared result (`PLang/app/goal/step/action/this.Schema.cs:65-69`): `Task<Data<T>>` names T; a bare `Task<Data>` or `Data<object>` is `item`, the unconstrained type. The catalog reads it; no separate attribute names it.

## Data is the carrier

`data.@this` (`PLang/app/data/this.cs`) is the result and the value carrier: a value with its name, type, properties, error and success. A carrier passes on whole and is opened (`await x.Value()`) by whoever needs the value, when it needs it. A `%variable%` in a parameter is resolved at that opening, not before (`Documentation/v0.2/decisions.md`, `no-ascanonical-in-dispatch`).

## Where the events and clauses come from

`on.start.Before` and `on.start.After` run at every level (`goal/this.cs:422`, `step/this.cs:143`, `action/this.cs:217`). A clause written in a step (`on error …`, `cache for …`, `timeout after …`) is an action of the `on` module that attaches to the action before it; see `clause.@this` (`action/this.cs:244-267`).
