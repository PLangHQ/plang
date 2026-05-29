# coder v1 — mathhelper-deletion — retype unary/comparison math handlers

**Branch off:** `plang-types` (depends on the Stage 4 `number.@this` value
type + Wrap envelope; will land on top of plang-types once that merges).

## What shipped

- **Deleted `PLang/app/modules/math/MathHelper.cs`** — the
  `ToDouble`/`PreserveType` helpers that backed the legacy
  `double`-then-coerce-back path.
- **Retyped 7 handlers** to `Task<Data<number>>`, routing through
  `number.*` with the same `Wrap` envelope as the arithmetic family:
  `abs`, `floor`, `ceiling`, `sqrt`, `round`, `min`, `max`.
- **New `PLang/app/types/number/this.Unary.cs`** adds the corresponding
  methods on `number.@this`:
  - `Abs(a)` — preserves Kind; `Int.MinValue` lifts to Long (Math.Abs(int)
    throws); `Long.MinValue` surfaces `MathOverflow`.
  - `Floor(a)` / `Ceiling(a)` — no-op on Int/Long; Math.Floor/Ceiling for
    Decimal/Double.
  - `Sqrt(a)` — always Double; negative input throws `ArithmeticException`
    (handler surfaces friendlier `InvalidInput` first).
  - `Round(a, decimals)` — kind-preserving, `MidpointRounding.AwayFromZero`.
  - `Min(a, b, policy)` / `Max(a, b, policy)` — promote via
    `PromoteKind` then return same-kind result, identical pattern to
    Add/Subtract.

## Namespace move (Ingi's call)

`app.modules.math.number.Config` → `app.modules.environment.number.Config`.

Reason: the lowercase `number` alias for `app.types.number.@this` is the
project convention (matches PLang's type name). Inside
`namespace app.modules.math`, the existing child namespace
`app.modules.math.number` shadowed the alias — any `data.@this<number>`
read as the namespace. Moving the Config file to
`app.modules.environment.number` removes the child namespace from
`app.modules.math` and the alias works everywhere.

PLang config key shape is unchanged (`ResolvePrefix<Config>` uses the
last namespace segment): `number.Overflow` / `number.Precision`. The
move reflects the design — number arithmetic policy is an app-wide
environment knob, not a math-module concern. Docstring updated to read
`- set environment.number.overflow = throw`.

## Style cleanup

All math handlers now use `using number = global::app.types.number.@this;`
(lowercase) matching the PLang type name. Previously they used
`using Number = ...;` (Pascal). The lowercase variant is now possible
because `app.modules.math.number` no longer exists as a sub-namespace.

## Tests

- **`MathHandlerDataReturnTests.cs`** — the 2 deferred `[Skip]` tests
  collapsed into one real assertion that `app.modules.math.MathHelper`
  is **absent** from the production assembly. Plus 7 new
  `Math<Op>_RunSignature_ReturnsDataNumber` tests (one per retyped
  handler) reusing the existing `AssertRunReturnsDataNumber` helper.
- **`RuntimeDoubleWrapTests.cs`** — `EveryDataObjectRunHandler_IsKnownToThisTest`
  expected-list trimmed: the 7 unary/comparison handlers no longer
  return `Task<Data<object>>`. Only `math.Random` still does (slated for
  its own retype later); the rest of the inventory is unchanged.
- **`NumberUnaryTests.cs` (new)** — 14 tests pinning `number.Abs/Floor/
  Ceiling/Sqrt/Round/Min/Max` behavior: Kind preservation, IntMinValue
  promotion, LongMinValue overflow surfacing as `MathOverflow`, Decimal
  rounding, mixed-kind Min/Max promotion via PromoteKind.

## Verification

- Clean build: `0 Error(s)` on PlangConsole + PLang.Tests.
- **C#: 3632 / 3640 pass, 0 fail, 8 skip** (was 3618 / 10 skip — 2
  MathHelper deferrals collapsed into 1 real, 14 new unary tests, 7
  new handler signature tests).
- **plang: 248 / 248 pass.**

## Files

- Deleted: `PLang/app/modules/math/MathHelper.cs`
- Moved: `PLang/app/modules/math/number/Config.cs` →
  `PLang/app/modules/environment/number/Config.cs`
- Modified: all 7 unary handlers + 5 existing arithmetic handlers
  (alias rename); `MathPolicy.cs` (namespace ref).
- New: `PLang/app/types/number/this.Unary.cs`
- New tests: `PLang.Tests/App/Types/NumberUnaryTests.cs`
- Modified tests: `MathHandlerDataReturnTests.cs`,
  `RuntimeDoubleWrapTests.cs`.

## For next bot (tester/security)

- The `number.Abs/Min/Max/etc.` methods route through the existing
  `Wrap` envelope unchanged — `MathOverflow`, `DivideByZero`,
  `ArithmeticError`, `PowerExponentTooLarge` keys all still surface.
- The PLang config key for arithmetic policy is `number.Overflow` /
  `number.Precision` (unchanged from plang-types HEAD); the
  `environment.number` C# namespace move is invisible to PLang prose.
- No PLang goal under `Tests/` exercises the unary math actions today.
  Worth pinning a smoke goal (`math.abs A=-5, write to %r%; assert %r%
  equals 5`) if the tester wants end-to-end coverage.
