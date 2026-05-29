# Tester — summary (latest: v5)

## Version
v5 — verifying coder v5 (`1cdb0a840`), the response to tester v4's needs-fixes (F1–F4).
**VERDICT: PASS.**

## What this is
`plang-types` adds a typed value system. tester v4 returned needs-fixes on:
- F1 (major) — Loader sealed-name security gate tested at 1 of 3 enforcement sites.
- F2 (minor) — sqrt handler test asserted only `Success.IsFalse`.
- F3 (process) — no baseline-tests.md for v3/v4.
- F4 (doc) — stale `this.Unary.cs` Sqrt docstring.

## What was done (this review)
Clean rebuild. C# 3636/3636, plang 248/248 (the v4 `StreamCallback` 502 did not recur —
confirms it was external infra). All four findings resolved, and the two highest-risk
(review-driven) tests were **independently mutation-confirmed by the tester**:

- **F1:** coder added `SignatureRendererShadow` (pass-2 renderer gate) + `CallbackInferredShadow`
  (pass-1 inferred-name gate) fixtures and two tests. Tester reproduced the mutation: neutralize
  `Loader.cs:122` + `:101`, leave `:88` intact → exactly those 2 tests fail (of 11), explicit-attr
  test stays green as control. Each asserts `TypeLoadCollision` + the sealed name in the message.
- **F2:** handler test now asserts `Error.Key == "ArithmeticError"`. Tester mutation (DoSqrt throws
  `DivideByZeroException`) now turns it red — it was green under the same mutation in v4.
- **F3/F4:** baseline written; docstring corrected.

Only residual: `coder/v5/report.md` is empty (non-blocking process nit).

## Code example (the control that proves the tests are honest)
```
// Mutation: Loader.cs:122 + :101 neutralized; :88 (explicit [PlangType]) left intact.
// RuntimeTypeLoadingTests → 2 of 11 fail:
//   FAIL LoadDll_SealedNameAsRendererTypeName   (gate :122 gone)
//   FAIL LoadDll_InferredSealedName             (gate :101 gone)
//   PASS LoadDll_AttemptToShadowSealedName      (gate :88 untouched — control)
```

## Outputs
- `.bot/plang-types/test-report.json` (shared) — findings F1–F4 marked resolved, F5 nit.
- `.bot/plang-types/tester/v5/{plan,result,verdict}.{md,json}`.

## Next
Branch is test-quality clean. Hand to security or merge per the pipeline.
