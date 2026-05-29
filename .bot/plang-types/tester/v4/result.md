# Tester v4 — result

**Scope:** coder v4 + the `mathhelper-deletion` merge landed since tester v3 PASS
(`cb258d222..a58dcfeee`).

## Test runs
- **C#:** 3634 / 3634 pass, 0 fail, 0 skip (clean rebuild per stale-binary protocol). Matches coder claim.
- **plang:** run 1 = 0 FAIL (green). run 2 = 1 FAIL on `Modules/Http/StreamCallback`
  with `502 Bad Gateway` (24.5s). External-endpoint flake; coder v4 touched no HTTP
  code. Re-run recommended; not a coder-v4 regression.
- Builder validated working end-to-end (LLM build of root `Start` succeeded, 9.3s).

## What I verified is GOOD
- **Power CPU-DoS cap** (`number/this.Arithmetic.cs`). The codeanalyzer-F1 rework
  (move the cap from the top of `DoPower` into the 3 CPU-loop branches via
  `EnsureExponentInRange`; let Double-base + fractional route through constant-time
  `Math.Pow`) is correctly scoped, and the tests pin it well:
  - `Power_ExponentAtCap_SmallBase_StillSucceeds` (boundary |exp|==64 allowed)
  - `Power_ExponentJustOverCap_TypedFailure_PowerExponentTooLarge` (cap+1 → typed Fail)
  - `Power_NegativeExponentBeyondCap_DecimalPrecision_TypedFailure` (Strict path)
  - `Power_DoubleBase_LargeExponent_SkipsCap_UsesMathPow` + `Power_NegativeExponent_DoubleBase_SkipsCap`
    + `Power_FractionalExponent_NotSubjectToCap` — pin that the uncapped paths stay uncapped.
  No finding. Each branch with a loop is guarded; each constant-time branch is pinned as skip.
- **Loader pass-1 explicit-attribute gate** is honestly tested via the committed
  `IdentityShadow.dll` (real Assembly.LoadFrom roundtrip), asserting Success=false,
  ErrorKey="TypeLoadCollision", ErrorMessage contains "identity". The test distinguishes
  TypeLoadCollision from TypeLoadCoverage (collision fires in pass 1, before the coverage gate).

## Findings (mutation-confirmed)

### F1 — major — Loader sealed gate tested at 1 of 3 sites
`Loader.Register` enforces `SealedNames` at three places:
- `:88` pass-1 explicit `[PlangType("identity")]`  — **tested** (IdentityShadow.dll)
- `:101` inferred-name (`@this`-convention) branch  — **untested**
- `:122` ITypeRenderer-registration pass            — **untested**

`SealedNames_AreCaseInsensitive_AndCoverCoreSigningTypes` only inspects the set's
contents — it does not drive `Register`, so it covers no enforcement site.

**Mutation:** I neutralized both `:101` and `:122` (`if (false && SealedNames.Contains(...))`),
rebuilt, and ran the 9 `RuntimeTypeLoadingTests`. **All 9 green, 0 failed.** Confirms zero
coverage on those two sites. The renderer site is the material one: the `SealedNames`
docstring names renderer-substitution as the attack ("replaced identity's CLR type **or its
renderer** could produce authentically-signed envelopes whose body was attacker-composed"),
yet no test loads a renderer for a sealed name. This is the "review-driven code is highest
risk" pattern — the coder added the gate for a security finding and tested only the first of
its three doors.

**Fix:** add (a) a fixture `ITypeRenderer{ TypeName="identity" }` → Register returns
TypeLoadCollision; (b) an inferred-name fixture (a `this`-named class whose namespace leaf is
a sealed name) → TypeLoadCollision.

### F2 — minor — sqrt handler test asserts only Success
`MathTests.Sqrt_NegativeInput_Fails` (line 137) asserts only `result.Success.IsFalse()`.
The codeanalyzer-F2 fix changed the negative-sqrt key from `InvalidInput` → `ArithmeticError`.

**Mutation:** forced `DoSqrt` to throw `DivideByZeroException` (key `DivideByZero`).
`Sqrt_NegativeInput_Fails` stayed **green**; `NumberUnaryTests.Sqrt_NegativeNumber_SurfacesArithmeticError`
went **red** (it asserts `Error.Key == "ArithmeticError"`). So the F2 behavior is genuinely
pinned — at the number layer — and the handler test is merely weak. Add an `Error.Key` assert
to the handler test to pin the handler-boundary contract too.

### F3 — minor (process) — no baseline-tests.md for v3/v4
Only coder v2 wrote `baseline-tests.md`. Per the tester workflow, a missing baseline is a
process violation to flag. Low impact this turn (v2 baseline was all-green), but attribution
of red↔regression should not depend on a stale baseline.

### F4 — minor (doc drift) — stale Sqrt docstring
`this.Unary.cs:13-15` still says negative sqrt "surfaces a typed validation error inside the
handler, not this method." F2 inverted this (handler pre-check deleted; `DoSqrt` now throws).
The stale comment could prompt a future contributor to re-add the double-guard F2 removed.

## Verdict
**needs-fixes** — F1 (add the two missing sealed-gate tests) + F2 (strengthen the handler
assertion). F3/F4 are minor. The plang 502 is flaky external infra, not actionable for the coder.
