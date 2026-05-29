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

## Scopes

The runtime's existing `app.config` walk covers three scopes — context (current), parent contexts (chain), and app defaults (`App.Config.Defaults`). Step scope sits below as a per-action override. Precedence: **step → context.ConfigScope → parent.ConfigScope → … → App.Config.Defaults → record default**.

| Scope | How it's set | Where it lives | Lifetime |
|---|---|---|---|
| Step | nullable action param (`overflow=throw`) | `Action.Overflow` / `Action.Precision` | One call |
| Context (and parent chain) | `- set math.number.overflow = throw` | `context.ConfigScope` (lazy) | Until context dies |
| App default | `- set math.number.overflow = throw, default: true` | `App.Config.Defaults` | App lifetime |
| Built-in | record property default (`Promote` / `Double`) | `number.Config` record | (compile-time) |

Step scope is the finest-grained and lives only for one action call — a nullable property on the action record, set by the LLM at compile time per the developer's per-step prose. All other scopes flow through `app.config`'s existing walk.

No ambient state. No `AsyncLocal`. No `Goal`-private overlay. `app.config`'s walk is explicit through `context.Parent` — debuggable, testable, no surprise behavior across thread / await boundaries.

## Scope storage — `app.config`

Policy lives in the runtime's existing config mechanism, `app.config` (`PLang/app/config/this.cs`) — not a new `environment` tree and not a `Goal`-private overlay (`Goal` isn't guaranteed thread-safe; a goal-stored overlay would be the wrong place for it).

How `app.config` solves it:

- `Context.ConfigScope` is the per-context scope (created lazily when a settings handler writes).
- Resolution walks `context.ConfigScope → context.Parent.ConfigScope → … → App.Config.Defaults → classDefault`.
- `IConfig` marker + `app.config.For<T>(context)` returns a context-bound view that reads keys with the module prefix already applied.

For number, this means:

```csharp
namespace app.modules.math.number;

public sealed record Config : IConfig
{
    public OverflowMode  Overflow  { get; init; } = OverflowMode.Promote;
    public PrecisionMode Precision { get; init; } = PrecisionMode.Double;
}
```

Resolution at a math handler:

```csharp
[Action("add")]
public partial class Add : IContext
{
    public partial Data<number> A { get; init; }
    public partial Data<number> B { get; init; }

    public partial OverflowMode?  Overflow  { get; init; }   // step override; nullable IS the optional marker
    public partial PrecisionMode? Precision { get; init; }

    public Task<Data<number>> Run()
    {
        var view   = Context.App.Config.For<number.Config>(Context);
        var policy = new NumberPolicy
        {
            Overflow  = Overflow  ?? view.Overflow,
            Precision = Precision ?? view.Precision,
        };
        // number.Add returns Data<number> — it catches OverflowException
        // internally and returns Data.Fail("MathOverflow"). The handler relays.
        return Task.FromResult(number.Add(A.Value, B.Value, policy));
    }
}
```

`view.Overflow` walks `ConfigScope → parent → Defaults → record default` — three of the four scopes for free (context, parent-context, app). Step scope is the local action parameter. Nothing is stored on `Goal`.

Sub-goal inheritance falls out for free: `app.config` walks the parent chain by construction, so a parent context's `Overflow=Throw` is visible to children unless they shadow it — the path of least surprise, and how every other `IConfig` in the runtime already behaves.

## Step-level: action parameters

`Overflow` / `Precision` ride as nullable enum properties on the action record (see Run() shown above). **`?` is the optional marker** — no `[Optional]` attribute needed. The LLM sees them as ordinary enum-valued action parameters in the catalog. Most calls won't set them — the action prose teaching layer (markdown files under `os/system/modules/math/`) gives the LLM the rule: set only when the developer explicitly asks for strict or lenient behavior on this step.

## The settings action surface

Setting policy lives on the existing settings-handler shape (`app.config.Set`-style), not a new `environment.set` action. A developer writes:

```
- set math.number.overflow = throw
```

The settings handler resolves the module prefix (`math.number` → `number.Config`), parses the value into `OverflowMode`, and writes to `context.ConfigScope` (context-scoped by default) or `app.Config.Defaults` (when `Default: true`). The walking handles propagation across sub-goals; no per-scope action variant is needed.

## What this is NOT

- **Not a new `environment` config tree.** Reuse `app.config`. `number.Config : IConfig` is the typed config object.
- **Not stored on Goal.** Goal isn't thread-safe; the `ConfigScope` lives on `Context`, which has a clear ownership model.
- **Not block-scoped.** Step scope is the finest-grained, lives only for one action call, set as a nullable property on the action record.
- **Not thread-local or AsyncLocal.** The walk is explicit via `context.Parent`.

## The 18-digit precision question

Crypto-currency values can carry 18 decimal points. The default `Precision = Double` mode loses precision past ~15 significant digits (IEEE-754 has 52 mantissa bits). For a value held as `decimal` that meets a `double` operand in any arithmetic, the **default policy promotes to double and truncates** — the lossy path.

Three responses:

1. **The escape hatch already exists.** `Precision = Decimal` keeps the value in `decimal` slot through arithmetic; `decimal` carries 28–29 significant digits, so 18 fits with room. The crypto handler sets `precision=decimal` (either on the math step or as a Config default for that goal). Lossless for everything decimal can hold.

2. **`decimal` × `double` is the dangerous boundary.** If someone holds a price as `decimal(1.123456789012345678)` and multiplies by a `double` factor, even with `Precision=Decimal` the double operand can't faithfully represent itself — the double already lost precision before the multiplication. The policy can refuse to promote (throw on the cross-kind op), or convert through `Convert.ToDecimal(double)` (lossy at the boundary, but bounded). Today's draft does the latter via `Decimal + Double → Decimal` with a throw on NaN/Infinity/out-of-range. Worth flagging in the user-facing docs: "crypto-grade precision means avoid `double` literals anywhere in your chain."

3. **`BigInteger` / arbitrary-precision is out of scope for this branch.** Past ~28 digits even `decimal` runs out. A `bignumber` type with `System.Numerics.BigInteger` (integer-only) or a `Mantissa + Exponent` decimal-extended type would cover the long tail. Naturally fits the `number` umbrella as a fifth `NumberKind` (`BigInteger`), but it's a future addition — design once 18-digit decimal feels like a real ceiling, not now. Flagged in [storage.md](storage.md) at the kinds discussion.

For day-one shipping: lenient default (`Double`) for general code, switch to strict (`Decimal`) at the action or config level for crypto / finance. Document explicitly. If 28 digits is a real ceiling later, `BigInteger` slots in without a structural change to the policy resolver — just a new kind, new promotion-table row.
