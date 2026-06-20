# app.goal.step.action

An action is the **leaf** — the bottom of the start cascade, where work actually
happens. `file.read`, `output.write`, `variable.set`: each does one thing. Above
it everything is courier (goal, step, the lists forward `start()`); the action is
where a `Data` is finally opened and a value produced.

```csharp
abstract class action {
    public abstract Task<data.@this> start();
}
```

`action` is abstract; every module operation is a concrete subclass. The base
promises one thing — `start()` — and the leaf delivers it.

## the leaf

Two things make an action a leaf (OBP rule 7):

- **It declares its input types.** A concrete action takes `Data<T>` parameters
  and may call `.Value()` on them — it named the type, so it owns the open.
- **It touches run state through `IContext`.** An action that reads `%variables%`
  or the current goal implements `IContext`; the runtime injects `Context`. A
  step or a list never does this — only the leaf that needs it.

```csharp
class read : IContext {
    public context Context { get; set; }              // injected
    public Task<data.@this<text.@this>> start() {     // known type → Data<text>
        var path = Context.variable.Get("path");
        ...
    }
}
```

An action returns `Data<T>` when its value is typed (`file.read` → `Data<text>`,
`math.add` → `Data<number>`) and bare `Data` only when genuinely polymorphic.
The return type is the action's promise — see [obp](../../../obp/start.md) rule 2.

## the collection: action.list

`action.list` owns the loop for a step. It runs each action in order, stops at
the first error, and returns the **last** result — so the step carries the value
its last action produced:

```csharp
public async Task<data.@this> start() {
    data.@this result = data.@this.Ok();
    foreach (var a in await actions.Value()) {
        result = await a.start();
        if (!result.Success) return result;   // short-circuit on error
    }
    return result;                            // the last action's value flows up
}
```

It is the list that forwards its last element's value rather than a bare `Ok()`:
a step's result *is* its last action's result.

## structure

Generated from the source at build time — never hand-maintained:

[[app/goal/step/action/start.json]]

There is no `step.actions`. The element is `action` (`@this`); the collection is
`action.list` (`list.@this`). A class named `list` cannot also have a `list`
member, so the collection exposes its backing as the `.list` property.

## OBP rules

- **The action is the leaf.** Only here is a `Data<T>` opened and a value made.
  Goal, step, and the lists are couriers — they forward `start()`, never open `Value`.
- **Declare the type.** Inputs are `Data<T>`; a typed result is `Data<T>`, never bare.
- **Context by `IContext`, never passed.** Only the leaf that touches run state asks.
- **The list owns the loop.** `action.list.start()` iterates and short-circuits;
  the action does one thing.

## source

`app/goal/step/action/start.cs`
