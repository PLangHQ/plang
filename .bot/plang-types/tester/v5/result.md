# Tester v5 — result

**Scope:** coder v5 (`1cdb0a840`) — the response to tester v4's needs-fixes (F1–F4).

## Test runs (clean rebuild, stale-binary protocol)
- **C#:** 3636 / 3636 pass, 0 fail, 0 skip (was 3634; +2 sealed-gate tests). Matches coder claim.
- **plang:** 248 / 248 pass, 0 fail. The v4 `StreamCallback` 502 did **not** recur — confirms
  it was external infra, as judged.

## Each v4 finding — verified by independent mutation (not the coder's claim)

### F1 (major) — RESOLVED. Sealed gate now tested at all 3 sites.
coder added two fixtures + two tests:
- `LoadDll_SealedNameAsRendererTypeName_FailsWith_TypeLoadCollision` (pass-2 renderer gate,
  `SignatureRendererShadow.dll`, ITypeRenderer TypeName="signature", no `[PlangType]`).
- `LoadDll_InferredSealedName_FailsWith_TypeLoadCollision` (pass-1 inferred-name gate,
  `CallbackInferredShadow.dll`, `@this` class in namespace `*.callback`).

**My mutation (reproduced):** neutralized `Loader.cs:122` (renderer) AND `:101` (inferred) with
`if (false && …)`, left `:88` (explicit) intact, rebuilt, ran RuntimeTypeLoadingTests →
**exactly 2 of 11 failed**: `…SealedNameAsRendererTypeName` and `…InferredSealedName`. The
explicit-attribute `LoadDll_AttemptToShadowSealedName` stayed GREEN (control — its gate was
untouched). So each new test pins its own gate site, and they don't pass for a shared/wrong
reason. Both also assert `ErrorKey=="TypeLoadCollision"` + `ErrorMessage` contains the sealed
name, so they can't slip onto the `TypeLoadCoverage` path.

### F2 (minor) — RESOLVED. Handler-boundary key now pinned.
`MathTests.Sqrt_NegativeInput_Fails` now asserts `Error.Key == "ArithmeticError"`.
**My mutation (reproduced):** forced `DoSqrt` to throw `DivideByZeroException` → the test
**failed** at the `Error.Key` assertion (`Expected to be equal to "ArithmeticError"`). The same
mutation left this test GREEN in v4 — direct proof the strengthening bit.

### F3 (process) — RESOLVED. `coder/v5/baseline-tests.md` written (a58dcfeee, all-green).

### F4 (doc) — RESOLVED. `this.Unary.cs` Sqrt paragraph now reads "negative input throws
ArithmeticException → Wrap → ArithmeticError, one canonical key across direct call and the
math.sqrt handler boundary." The misleading "validation error inside the handler" wording is gone.

## Residual (non-blocking)
- `coder/v5/report.md` is an empty file (the commit message carries the detail). Minor process
  nit — does not affect test quality.

## Verdict
**PASS.** All four v4 findings resolved and the two highest-risk (review-driven) tests are
mutation-confirmed by me to fail when their target gate breaks. No false greens remain.
