# Data.Value firing — where Name and Path come from

## The hook

```csharp
public T Value
{
    get
    {
        Context.event.Before(On.Variable, this).GetAwaiter().GetResult();
        var v = Resolve(value);
        Context.event.After(On.Variable, this).GetAwaiter().GetResult();
        return v;
    }
    set
    {
        Context.event.Before(On.Variable, this).GetAwaiter().GetResult();
        ApplyValue(value);
        Context.event.After(On.Variable, this).GetAwaiter().GetResult();
    }
}
```

(The sync-over-async shape is sketch — actual implementation needs to
decide whether `.Value` stays sync and we use a sync-firing path on
Event.@this, or `.Value` becomes async. See "Sync vs async firing"
below.)

Inside `Context.event.Before/After`:
- Read `source.Name` and `source.Path` from the Data argument.
- Look up bindings under `(On.Variable, source.Name)` in the registry's index.
- Filter by `source.Path` (glob/regex).
- Filter by phase.
- Run each matching binding's handler in priority order.
- Consult `App.Event.@this` for `scope:app` bindings too.

## Where Name and Path get set

Two-Data world: the variable resolver wraps values, and that's where
Name and Path get stamped.

### Resolving `%step.Text%`

The resolver walks the dot-path:

```csharp
// Pseudocode
Data Resolve(string expression, Context ctx)
{
    var segments = SplitDots(expression);     // ["step", "Text"]
    var root = ctx.Variables[segments[0]];    // Data wrapping the step variable
                                              //   already has Name="step", Path=null
    if (segments.Length == 1) return root;

    var pathSoFar = "";
    var current = root;
    for (int i = 1; i < segments.Length; i++)
    {
        pathSoFar = pathSoFar == "" ? segments[i] : pathSoFar + "." + segments[i];
        var next = AccessMember(current.Value, segments[i]);
        // Wrap in new Data, propagating Name from root, accumulating Path
        current = new Data<object>(next, name: root.Name, path: pathSoFar, context: ctx);
    }
    return current;
}
```

Resulting Data for `%step.Text%`: `Name="step"`, `Path="Text"`.

Resulting Data for `%step%` (no dot): `Name="step"`, `Path=null`.

Resulting Data for `%step.Action.Module%`: walk produces three Datas
in sequence:
- Intermediate Data 1: `Name="step"`, `Path="Action"`
- Intermediate Data 2: `Name="step"`, `Path="Action.Module"`

Each `.Value` access on those fires its own Variable event.

### Result/literal Data has no meaningful Name

Data constructed from an action's return, a literal, or a computed
expression doesn't correspond to a named variable. Two options for
its Name:

- **(A) Sentinel** — `Name = ""` (or `"_"`). The registry index has
  no bindings under that key (or the lookup short-circuits when name
  is empty). Cheap, predictable.
- **(B) Synthesized** — name like `"_return_FooAction"` for return
  data. Bindings can match it if user wants, but the naming is
  unstable and probably not what users target.

**Lean (A).** Empty string sentinel. The fast path inside
`Event.@this.HasAnyBindingsFor(On.Variable, "")` returns false in O(1).
Result Data flies through `.Value` with one bool check overhead.

The `Name=""` rule: result/literal Data never matches a binding.
This is the right semantics — users register on named variables, not
on the engine's intermediate values.

## Path semantics for intermediate walks

`%step.Action.Module%` fires:
1. Intermediate `(On.Variable, "step", "Action")` events at the
   `step.Action` walk step.
2. Final `(On.Variable, "step", "Action.Module")` events at the
   leaf.

A binding `{on:Variable, name:"step", path:"Action"}` fires for #1.
A binding `{on:Variable, name:"step", path:"Action.*"}` fires for both.
A binding `{on:Variable, name:"step", path:"Action.Module"}` fires only for #2.

Path is a glob/regex pattern, same as name.

## Sync vs async firing

`Data.Value` is a property — synchronous by .NET convention. But
handlers run goals, which are async. Two ways to bridge:

- **(I) Keep `.Value` sync, fire synchronously.** Event.@this has a
  sync `Before`/`After` overload that runs handlers blocking. Wrong
  for handlers that do IO, but Variable events are typically fast
  observation (logging, conditional traces). Acceptable as a v1
  constraint: variable-event handlers MUST be sync (or be willing to
  block their caller). Document it.
- **(II) Make `.Value` async** — `ValueTask<T> Value { get; }` or a
  separate `ValueAsync()` method. Breaks every existing caller. Big
  ripple.
- **(III) Two surfaces** — `.Value` sync (no event firing) and
  `.ValueAsync()` async (fires events). PLang variable resolution
  routes through `ValueAsync`; legacy/internal C# uses `.Value`.
  Compatible with "PLang-only firing" rule. Probably the right shape.

**Lean (III).** Coder: confirm with Ingi which one to implement.
(III) preserves the rule that C# direct property access doesn't fire,
while keeping handler async semantics intact.

## What happens when no bindings exist

Per [registry-internals.md](registry-internals.md): the fast path on
`Event.@this` is a class-level dirty-bit check (`HasAnyBindingsFor(On
on)`). With zero `On.Variable` bindings registered anywhere, the
dispatcher inside `Context.event.Before` short-circuits:

```csharp
if (!HasAnyBindingsFor(On.Variable) && !App.HasAnyBindingsFor(On.Variable))
    return CompletedTask;
```

Two hashset hits and a return. Steady-state cost for variable-event
firing with no bindings: ~5ns. Indistinguishable from raw property
access at the variable-resolver granularity.

## Why this hook is in `Data.Value` and not the resolver

Alternative considered (and rejected per Ingi 2026-05-12 conversation):
the resolver fires events when it constructs a Data, not when `.Value`
is read. Two reasons that loses:

1. **Reading `.Value` is the actual "use" moment.** A Data may be
   constructed and never read (filtered out, short-circuited). Firing
   at construction fires events for accesses that didn't happen.
2. **A Data may be read multiple times.** Firing in the getter
   captures each access; firing at construction captures only one.

Putting the hook in `.Value` is more semantically honest: events fire
when the value is actually consumed.
