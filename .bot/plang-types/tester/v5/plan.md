# Tester v5 — plan

Verifying coder v5 (commit `1cdb0a840`) — the response to tester v4's needs-fixes.

## What coder v5 changed
- **F1:** +2 tests + 2 fixture DLLs.
  - `LoadDll_SealedNameAsRendererTypeName_FailsWith_TypeLoadCollision` (pass-2 renderer gate,
    `SignatureRendererShadow`, TypeName="signature").
  - `LoadDll_InferredSealedName_FailsWith_TypeLoadCollision` (pass-1 inferred-name gate,
    `CallbackInferredShadow`, `@this` in namespace `*.callback`).
- **F2:** `MathTests.Sqrt_NegativeInput_Fails` now asserts `Error.Key == "ArithmeticError"`.
- **F4:** `this.Unary.cs` Sqrt docstring corrected.
- **F3:** `coder/v5/baseline-tests.md` written.

## Verification (don't trust the coder's mutation claim — reproduce it)
1. Clean rebuild (stale-binary protocol).
2. Full C# + plang suites; diff vs v5 baseline (3634 C# / 248 plang green).
3. **Mutation A** — neutralize the pass-2 renderer gate (`Loader.cs:122` → `if (false && ...)`).
   Expect `LoadDll_SealedNameAsRendererTypeName_*` to go RED; others green.
4. **Mutation B** — neutralize the inferred-name gate (`Loader.cs:101`).
   Expect `LoadDll_InferredSealedName_*` to go RED.
5. **Mutation C** — wrong sqrt key. Expect `Sqrt_NegativeInput_Fails` now goes RED (was green in v4).
6. Watch for false-green-by-wrong-reason: each sealed test must fail with TypeLoadCollision
   specifically, not slip on TypeLoadCoverage or a fixture-load error.

## Process notes to check
- `coder/v5/report.md` is empty — minor process note.
