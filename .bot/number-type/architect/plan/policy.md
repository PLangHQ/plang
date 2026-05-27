# Policy — three scopes, lenient defaults, no ambient state

This file goes deep on how arithmetic policy is configured and resolved: the two axes, the three scopes, where the configs live, how the resolver walks precedence, and what the developer-facing surface looks like. The spine ([../plan.md](../plan.md)) locks the architectural decision; this locks the implementation contract.

## The two axes

```csharp
namespace app.types.number;

public readonly struct NumberPolicy
{
    public OverflowMode  Overflow  { get; init; }
    public PrecisionMode Precision { get; init; }

    public static NumberPolicy Lenient => new()
    {
        Overflow  = OverflowMode.Promote,
        Precision = PrecisionMode.Double,
    };

    public static NumberPolicy Strict => new()
    {
        Overflow  = OverflowMode.Throw,
        Precision = PrecisionMode.Decimal,
    };
}

public enum OverflowMode  { Promote, Throw }
public enum PrecisionMode { Double, Decimal }
```

**`Overflow`:**
- `Promote` (lenient default): `Int + Int` that overflows widens to `Long`; `Long + Long` widens to `Decimal`; `Decimal` overflow throws (no wider integer kind).
- `Throw`: any overflow throws immediately, no widening.

**`Precision`:**
- `Double` (lenient default): `Decimal + Double` promotes to `Double` — IEEE-754 wins, decimal precision lost past ~15 digits.
- `Decimal`: `Decimal + Double` stays `Decimal` — throws if the double operand is NaN / Infinity / out of decimal range.

## Three scopes

| Scope | How it's set | Where it's stored | Lifetime |
|---|---|---|---|
| App | `- set environment.number.overflow = throw` (scope omitted; default is `app`) | `App.Environment.Number` | App lifetime |
| Goal | `- set environment.number.overflow = throw, scope: goal` | `Goal.Environment.Number` (lazy overlay) | Until goal exits |
| Step | `- math.add %x% %y% overflow=throw` (action parameter) | `Action.Overflow` / `Action.Precision` properties | One call |

Step scope is the finest-grained and lives only for one action call. It's not a separate "set then call" — it's a parameter on the action itself, set by the LLM at compile time per the developer's per-step prose.

## Resolution

```csharp
public static NumberPolicy Resolve(
    OverflowMode?  stepOverflow,
    PrecisionMode? stepPrecision,
    environment.number.@this? goalScope,
    environment.number.@this   appScope)
{
    return new NumberPolicy
    {
        Overflow  = stepOverflow  ?? goalScope?.Overflow  ?? appScope.Overflow,
        Precision = stepPrecision ?? goalScope?.Precision ?? appScope.Precision,
    };
}
```

Precedence: **step > goal > app > built-in default**. The built-in default lives in `appScope` since `App.Environment.Number` is constructed with `NumberPolicy.Lenient` at App startup. `goalScope` may be null (the lazy overlay hasn't been created yet) — the null-conditional handles that path cleanly.

No ambient state. No `AsyncLocal<NumberPolicy>`. Every resolution is an explicit walk through arguments the caller passes — debuggable, testable, no surprise behavior across thread / await boundaries.

## App-level home

```csharp
namespace app.environment.number;

public sealed class @this
{
    public OverflowMode  Overflow  { get; set; } = OverflowMode.Promote;
    public PrecisionMode Precision { get; set; } = PrecisionMode.Double;
}
```

Mounted on `app.environment.@this`:

```csharp
namespace app.environment;

public sealed class @this
{
    public number.@this Number { get; } = new();
    // future siblings: text, time, culture, ...
}
```

`App` grows an `Environment` property of type `app.environment.@this`, constructed once at App startup. The `Number` child is non-nullable — always exists, always has lenient defaults until a step modifies it.

## Goal-level overlay — lazy

`Goal` grows an `Environment` overlay mirroring the existing `Events` pattern:

```csharp
// In app.goals.goal.this.cs — alongside the existing Events backing field
private app.environment.@this? _environment;

[System.Text.Json.Serialization.JsonIgnore]
public app.environment.@this? Environment
{
    get => _environment;                       // null until a step writes to it
    set => _environment = value;
}

// Plus a lazy-creating helper used by environment.set when scope=goal:
internal app.environment.@this EnsureEnvironment()
    => _environment ??= new app.environment.@this();
```

Distinction from `App.Environment`:

- `App.Environment.Number` always exists, never null. Holds the app-wide values.
- `Goal.Environment` is **null** by default. Created only when a step writes to it with `scope: goal`. Even then, individual children (`.Number`, future `.Text`, …) are only populated for the axes the goal actually touches.

When the goal exits and the goal frame is dropped, the overlay goes with it — no explicit cleanup. Sub-goals **do not inherit** their parent goal's overlay; if a parent goal sets `environment.number.overflow=throw, scope: goal` and calls a child goal, the child sees the App-level value, not the parent's overlay. (Goal-scope means "this goal's body," not "this goal and everything it calls.") Worth confirming with Ingi during Stage 2 — see open question at the bottom.

## Step-level: action parameters

Every `math.*` action grows two optional parameters:

```csharp
[Action("add")]
public partial class Add : IContext
{
    public partial Data<number> A { get; init; }
    public partial Data<number> B { get; init; }

    [Optional] public OverflowMode?  Overflow  { get; init; }
    [Optional] public PrecisionMode? Precision { get; init; }

    public Task<Data<number>> Run()
    {
        var policy = NumberPolicy.Resolve(
            stepOverflow:  Overflow,
            stepPrecision: Precision,
            goalScope:     Context.Goal.Environment?.Number,
            appScope:      Context.App.Environment.Number);
        var result = number.Add(A.Value, B.Value, policy);
        return Task.FromResult(Data<number>.Ok(result));
    }
}
```

The LLM sees `Overflow` / `Precision` as ordinary enum-valued action parameters in the catalog. Most calls won't set them — the action prose teaching layer (markdown files under `os/system/modules/math/`) gives the LLM the rule: set only when the developer explicitly asks for strict or lenient behavior on this step.

## The `environment.set` action surface

`- set environment.number.overflow = throw, scope: goal` is the developer-facing syntax. Three contracts:

1. **Parse the key path.** `environment.number.overflow` decomposes into namespace `environment.number` and property `overflow`. The action walks `App.Environment` (or `Goal.Environment` per scope) by namespace segment, finds the leaf property by name. Strongly-typed — invalid paths or missing properties are compile-time errors at build (validated by the action's Build hook), not runtime nulls.

2. **Parse the value.** Value is a string from the .goal source; the action coerces to the property's CLR type (`OverflowMode`, `PrecisionMode`, ...). For enum-typed properties this is `Enum.Parse(propertyType, value, ignoreCase: true)`.

3. **Apply the scope.**
   - `scope: app` (default) — writes to `App.Environment.Number.Overflow`.
   - `scope: goal` — writes to `Goal.EnsureEnvironment().Number.Overflow` (creates the overlay if needed; creates the `Number` child if needed).
   - `scope: step` — **invalid** for `environment.set`. Step-scoping happens via the math action's own parameters, not via a separate set. The action surfaces this as a typed build error.

Stage 2 owns the implementation; this plan locks the shape.

## What this is NOT

- **Not a generic settings system.** `environment.number` is a typed config object with typed enum-valued properties. Adding `environment.text` later for the text category is the same shape — typed object, typed properties. Nothing is dynamic dictionary-style.
- **Not block-scoped.** Step scope is the finest-grained, lives only for one action call, set on the action parameters. No `using (NumberPolicy.Strict)` C# blocks.
- **Not thread-local or AsyncLocal.** Explicit precedence walk via arguments. No hidden propagation through await points.
- **Not inherited across sub-goals by default.** Goal-scope means "this goal's body" — sub-goals start fresh from App scope. (See open question.)

## Open question for Stage 2

**Sub-goal inheritance.** If parent goal sets `environment.number.overflow=throw, scope: goal` and calls a child goal, does the child see the parent's overlay or the App-level value?

This plan stakes out "child sees App-level value" — the simplest and most predictable shape. The alternative ("child walks the goal-stack overlay chain") is more flexible but introduces a precedence rule that needs to be documented and held consistent across other future `environment.*` axes.

Defer to Stage 2 with a leaning toward "no inheritance." Worth a one-line confirm from Ingi before Stage 2 lands.
