# security v2 — plang-types — verify v1 fixes + scan delta

## Scope

Diff: `d963fcf55..b32fd0dfe` (security v1 PASS → now).

Coder v4 addressed v1 F1 + F2. Codeanalyzer v2 reviewed and coder addressed.
Tester v4 found 4 issues, coder v5 fixed all 4, tester v5 PASS.

`mathhelper-deletion` merge also landed in this window — retyped unary/comparison
math handlers (abs, ceiling, floor, sqrt, round, min, max) through `number.*`.

## Method

1. **Verify F1 fix** — `MaxPowerExponent` cap in `number.Arithmetic.DoPower` —
   re-read each CPU-loop branch, confirm `EnsureExponentInRange` covers all
   three (negative-exp decimal, integer-exp integer-base, integer-exp
   decimal-base) and that Math.Pow paths legitimately skip.
2. **Verify F2 fix** — `SealedNames` allowlist in `Loader.Register` — re-read
   the gate at all three sites (explicit `[PlangType]`, inferred name,
   `ITypeRenderer.TypeName`). Tester v5 has mutation-confirmed coverage at
   each site; my job is to confirm the gate logic is sound (not just
   exercised).
3. **Scan delta for new findings** — `mathhelper-deletion` retypes through
   `Wrap`-wrapped surface. Check each unary/comparison handler for parameters
   that could escape `Wrap`'s catch envelope (DivideByZero, Overflow,
   Arithmetic, PowerExponentTooLarge — but NOT ArgumentOutOfRangeException).
4. **Reaffirm F3 standing** — image byte intake cap; deferred per coder v4
   report, latent, no new shipping handler exposes it.
