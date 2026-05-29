# security — plang-types

## Version

v2 (verification of v1 fix cycle + delta scan of mathhelper-deletion merge).

## What this is

The plang-types branch lands the value-system spine: runtime DLL type-loading,
new `image`/`code`/`number` value types, per-(type, format) renderer dispatch,
math handler retype-through-`number.*`. v1 PASS flagged three Low findings;
coder v4 + codeanalyzer v2 + coder + tester v4 + coder v5 + tester v5 PASS
cycle landed in this window. v2 verifies the two coder-fixed findings and
audits the `mathhelper-deletion` merge that landed alongside.

## What was done

### v1 fixes verified

- **F1 (math.power exponent cap)** — `MaxPowerExponent = 64` const in
  `number.@this`, `EnsureExponentInRange` at all three CPU-loop branches in
  `DoPower`, Math.Pow branches deliberately skip. Tester v5 mutation-confirmed
  loop sites covered + Math.Pow sites pinned as skip. Closed.
- **F2 (Loader sealed-names allowlist)** — `SealedNames = {identity,
  signature, signedoperation, callback, channel}` (OrdinalIgnoreCase). Gate
  at all three sites: explicit `[PlangType]`, inferred `@this` name,
  `ITypeRenderer.TypeName`. Tester v5 mutation-confirmed each site via three
  real fixture DLLs (IdentityShadow, SignatureRendererShadow,
  CallbackInferredShadow). Closed.

### New scan — math handler family post-mathhelper-deletion

Every retyped handler routes through `number.* → Wrap`. Reviewed each.
Found one Low:

- **F4 (NEW)** — `math.round` with `Decimals` outside the framework-legal
  range (`[0, 28]` decimal, `[0, 15]` double) throws
  `ArgumentOutOfRangeException` from `System.Math.Round`. `number.Wrap`
  catches DivideByZero/Overflow/Arithmetic/PowerExponentTooLarge but not
  ArgumentException. Reachable from any goal with attacker-influenced
  `%user_decimals%`. Breaks the action-boundary typed-failure contract.
  Fix posture: clamp at `DoRound` entry, surface `ArithmeticError` for
  out-of-range.

### Standing

- **F3 (image byte cap)** — deferred per coder v4; latent (no shipping
  handler binds `Data<image>` from string), fix is architectural at the
  path-verb layer (consolidate `ReadBytes(maxBytes?)` alongside the standing
  OpenAI provider finding).

### Verdict

**PASS.** F1 + F2 closed with mutation-confirmed coverage. F4 is the only
new Low (non-blocking). F3 standing deferral acknowledged.

### Files written

- `.bot/plang-types/security-report.json` — v2 state (F1/F2 status=fixed,
  F3 deferred, F4 open)
- `.bot/plang-types/security/v2/v1_review_summary.md`
- `.bot/plang-types/security/v2/plan.md`
- `.bot/plang-types/security/v2/result.md`
- `.bot/plang-types/security/v2/verdict.json` — `{status: pass}`

## Code example

The new F4 pattern — the typed-failure contract breaks because Wrap doesn't
catch the framework's argument-range exception:

```csharp
// PLang/app/types/number/this.Unary.cs:84-90
private static @this DoRound(@this a, int decimals) => a.Kind switch
{
    NumberKind.Int or NumberKind.Long => a,
    // Math.Round(decimal, decimals) throws ArgumentOutOfRangeException when
    // decimals∉[0,28]. Wrap doesn't catch ArgumentException, so a Decimals=999
    // surfaces uncaught instead of mapping to ArithmeticError.
    NumberKind.Decimal => From(System.Math.Round(a.AsDecimal(), decimals, MidpointRounding.AwayFromZero)),
    NumberKind.Float or NumberKind.Double => From(System.Math.Round(a.AsDouble(), decimals, MidpointRounding.AwayFromZero)),
    _ => throw new System.InvalidOperationException(),
};
```

## For v2 after review

Reviewer (tester v4) flagged that coder v4's F2 fix tested 1 of 3 gate
sites. Coder v5 added two fixture DLLs + tests for the missing sites
(inferred-name and ITypeRenderer.TypeName). My v2 confirms the gate logic
is sound at all three sites (Unicode bypass ruled out, partial-state on
bail is sub-Low informational not a flaggable finding). Pattern: when a
finding becomes a gate, every entry point to that gate must be exercised —
catalog the entry points before testing.
