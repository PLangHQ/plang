# Tester v4 — plan

Reviewing coder v4 + the `mathhelper-deletion` merge that landed since my v3 PASS
(`cb258d222..a58dcfeee`).

## What changed (production)
1. **math.power CPU-DoS cap** (`number/this.Arithmetic.cs`) — security v1 F1.
   `MaxPowerExponent=64`. codeanalyzer v2 F1 then moved the check out of the top
   of `DoPower` into the 3 CPU-loop branches via `EnsureExponentInRange`; Double
   base + fractional exponent route through `Math.Pow` and skip the cap.
2. **Loader sealed-name allowlist** (`types/Loader.cs`) — security v1 F2.
   `SealedNames = {identity, signature, signedoperation, callback, channel}`,
   checked at 3 sites: PlangType registration, ITypeRenderer registration,
   inferred-name branch. Returns `TypeLoadCollision`.
3. **sqrt error-key unification** (`math/sqrt.cs`) — codeanalyzer v2 F2.
   Dropped handler pre-check (`InvalidInput`); now relies on `number.Sqrt` →
   `ArithmeticError`.
4. **mathhelper-deletion merge** — unary/comparison handlers retyped through
   `number.*`; `MathHelper.cs` deleted.

## Hypotheses to test (false-green hunt)
- **H1 (Loader gate sites):** only the PlangType-registration site is tested
  (IdentityShadow.dll). ITypeRenderer-registration site and inferred-name site
  are deletion-test-vulnerable. Security-relevant.
- **H2 (sqrt handler key):** `MathTests.Sqrt_NegativeInput_Fails` asserts only
  `Success.IsFalse` — the F2 change (InvalidInput → ArithmeticError) is NOT
  pinned at the handler boundary. Number-layer test covers the key.
- **H3 (power cap):** confirm the cap genuinely guards each loop branch and the
  Math.Pow-skip tests pin the codeanalyzer-F1 behavior change.

## Steps
1. Clean rebuild (done — stale-binary protocol). 
2. Run C# suite + plang suite, diff against coder baseline.
3. Deletion/mutation tests for H1, H2, H3.
4. Write findings + verdict.
