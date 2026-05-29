# codeanalyzer v2 — plang-types

**Verdict: PASS**
**Next bot:** none (security-spine fixes are landed; branch is ready for merge handoff to docs/architect at the user's discretion).

## Scope of v2

Re-review of the C# diff since `022ca0dc7` (codeanalyzer v1 PASS):

- coder follow-ups: codeanalyzer v1 minors, tester v1/v2 findings, security v1 F1+F2
- `mathhelper-deletion` merge: retype `abs/floor/ceiling/sqrt/round/min/max` through `app.types.number`, delete `MathHelper.cs`
- `environment/number/Config.cs` rename + config-prefix move

Files reviewed (production C# only):

```
PLang/app/types/Loader.cs                          (security F1 — SealedNames)
PLang/app/types/number/this.Arithmetic.cs          (security F2 — exponent cap, divide dead-code)
PLang/app/types/number/this.Unary.cs               (new — unary/comparison helpers)
PLang/app/types/renderers/this.cs                  (dead-store cleanup)
PLang/app/modules/math/{abs,ceiling,floor,round,sqrt,min,max}.cs
PLang/app/modules/math/MathHelper.cs               (deleted)
PLang/app/modules/math/MathPolicy.cs               (config namespace move)
PLang/app/modules/environment/number/Config.cs     (renamed)
```

Build: `dotnet build PlangConsole` → **0 errors**, warnings are pre-existing nullable-reference noise unrelated to this diff (counts unchanged vs. v1 baseline).

Banned-API scan on the diff (`System.IO.*`, `Console.*`): clean.
MathHelper references: zero in production C#; remaining hits are tests asserting the deletion.

## Findings

### F1 — `MaxPowerExponent = 64` cap fires on non-integer-base integer-exponent paths too (Note / minor)

**File:** `PLang/app/types/number/this.Arithmetic.cs:210`

The exponent-magnitude check sits before kind-dispatch, so `1.5^100` (Decimal base, integer exponent 100) and `2.0^200` (Double base) reject with `PowerExponentTooLarge` even though those paths would either be a constant-time `System.Math.Pow` (Double) or a 100-iteration Decimal multiply that is cheap by any DoS standard.

The cap is correctly load-bearing for integer-base integer-exponent and negative-integer Decimal paths (the `for (i = 0; i < expL; i++)` loops). For the Decimal-base positive-integer path at lines 248–253 it's also a loop, so the cap is defensible there too. For Double base at line 254 (`System.Math.Pow(a.AsDouble(), expL)`) the cap is purely defensive — the call would be constant-time without it.

Not a security regression (cap is conservative-safe) and the surfaced error is clear, so leaving it is fine. If the surprise ever bites a user, the targeted fix is to skip the cap on the Double-base path. Capture in `Documentation/v0.2/todos.md` if you don't want to act now.

### F2 — `math/sqrt.cs` checks negative input twice (Note / cosmetic)

**File:** `PLang/app/modules/math/sqrt.cs:14-18` + `PLang/app/types/number/this.Unary.cs:80-83`

The handler pre-validates with `n.ToDouble() < 0 → ValidationError("Cannot take square root of negative number")` and then calls `number.Sqrt(n)` which also throws `ArithmeticException` for the same case (wrapped by `Wrap` into `ArithmeticError`). Both paths are reachable in principle (different callers), so the double-guard isn't dead code, but the handler form is technically redundant — `number.Sqrt` already surfaces the error.

Keeping both is harmless. If you want one canonical error key for negative-sqrt, drop the handler pre-check and let `Wrap`'s `ArithmeticError` win.

### F3 — `SealedNames` is forward-looking; only `identity` is an active `[PlangType]` today (Informational)

**File:** `PLang/app/types/Loader.cs:46-50`

Of the sealed names `{identity, signature, signedoperation, callback, channel}`, only `Identity` (`PLang/app/modules/identity/types.cs`) currently bears `[PlangType]` and is discoverable via the loader. `Signature` (`PLang/app/modules/signing/Signature.cs`) is a plain class — not `[PlangType]`, not `@this`-conventioned, so a runtime DLL adding `[PlangType("signature")]` is the *only* way "signature" enters the catalog, and that path is now blocked. The other three are pre-emptive — no current catalog entry to shadow.

Forward-looking sealing is correct because the threat model is "DLL shadows a future built-in", not "DLL shadows today's built-in". But the list is hand-maintained — when a new signing-load-bearing type joins the catalog with `[PlangType]`, someone has to remember to add its name here. A short doc comment on `SealedNames` pointing to the maintenance trigger (e.g., "extend this when adding any `[PlangType]` class whose body is signing- or transport-load-bearing") would make the discipline harder to forget. The current comment names the *what* but not the *when-to-update*.

Suggest extending the existing XML doc; not blocking.

### F4 — `renderers/this.cs` dead-store cleanup (verified correct)

The removed `Convert.ChangeType` branch was unreachable in the pre-cleanup code — the first assignment to `del` was immediately overwritten by the `if/else` that followed (both arms ignored `Convert.ChangeType` entirely). The simplification preserves behavior and removes the misleading dead branch. Good cleanup.

### F5 — `DoDivide` self-cancelling ternary removed (verified correct)

`this.Arithmetic.cs:172-173` was previously `kind = policy.Precision == PrecisionMode.Decimal ? NumberKind.Decimal : NumberKind.Decimal` (both arms identical). Now `kind = NumberKind.Decimal` unconditionally. The XML doc on the file was updated in the same commit to reflect "Divide always promotes Int/Long to Decimal regardless of `PrecisionMode`" — code and doc agree. Good catch + clean fix.

## OBP / good_to_know spot-check on the new code

- `number/this.Unary.cs` is a partial-of-`@this` — same OBP shape as `this.Arithmetic.cs`, correctly placed. ✓
- Math handlers all return `Task<data.@this<number>>` (typed return per the catalog rule); no naked-`T` leaves. ✓
- `Identity` and the other sealed names use `[PlangType]` correctly; `InferName` lowercases class names so case-insensitive `SealedNames` comparison is right. ✓
- Config move keeps `IConfig` partial shape; PLang prose for `environment.number.overflow` matches the new namespace. ✓
- No `System.IO.*`, no `Console.*` in new code. ✓
- Renderers simplification — no behavioral change, dead store gone. ✓

## What v2 did not re-review

- C# tests under `PLang.Tests/App/` — tester v3 PASS covers these; codeanalyzer is not the test-shape reviewer.
- `.bot/` reports and `.pr` files — out of scope.
- `Tests/**/*.test.goal` / `*.test.pr` — out of scope (tester domain).

## Bottom line

The 5 production-code observations above are notes, not blockers. The security-spine fixes (SealedNames, exponent cap) are correct and proportionate. MathHelper deletion is clean — no lingering callers, the unary path through `number.*` preserves the int/long/decimal/double-track invariants from the v1 review. Branch is in mergeable shape from the codeanalyzer angle.
