# Tester — summary (latest: v4)

## Version
v4 — reviewing coder v4 + the `mathhelper-deletion` merge (`cb258d222..a58dcfeee`).
(v3 was PASS; v4 work landed after.)

## What this is
`plang-types` adds a typed value system. This turn the coder addressed:
- **security v1 F1** — a CPU-DoS cap (`MaxPowerExponent=64`) on `math.power` integer-loop paths.
- **security v1 F2** — a Loader sealed-name allowlist so a runtime-loaded DLL cannot shadow
  signing-load-bearing built-in types (`identity`, `signature`, `signedoperation`,
  `callback`, `channel`) → `TypeLoadCollision`.
- **codeanalyzer v2 F1** — scope the power cap to the loop branches only (Double-base /
  fractional route through constant-time `Math.Pow` and skip it).
- **codeanalyzer v2 F2** — drop the `math.sqrt` handler's pre-check; rely on `number.Sqrt`
  → one canonical `ArithmeticError` key.
- **mathhelper-deletion merge** — unary/comparison handlers retyped through `number.*`;
  `MathHelper.cs` deleted.

## What was done (this review)
Clean rebuild (stale-binary protocol). C# 3634/3634 green. plang green on run 1; run 2 hit a
single external `502 Bad Gateway` on `Modules/Http/StreamCallback` — flaky infra, not a
coder-v4 regression (coder touched no HTTP). Two false-green hypotheses **mutation-confirmed**:

- **F1 (major, missing-coverage):** the Loader sealed gate fires at 3 sites but is tested at 1.
  Neutralizing the ITypeRenderer-registration (`:122`) and inferred-name (`:101`) checks left
  all 9 `RuntimeTypeLoadingTests` green. Renderer-substitution is the documented threat → real gap.
- **F2 (minor, weak-assertion):** `MathTests.Sqrt_NegativeInput_Fails` asserts only
  `Success.IsFalse`; a wrong error-key mutation kept it green. The F2 key change is pinned
  at the number layer (`NumberUnaryTests`), so coverage exists — the handler test is just weak.
- **F3/F4 (minor):** missing `baseline-tests.md` for v3/v4; stale `this.Unary.cs` docstring.

Verdict **needs-fixes** on F1+F2. Power-cap tests are strong — no finding there.

## Code example (the mutation that exposed F1)
```csharp
// Loader.cs:122 — neutralized for the deletion test:
if (false && SealedNames.Contains(instance.TypeName))   // ITypeRenderer gate
    return new Result(false, "TypeLoadCollision", ...);
// + :101 inferred-name gate neutralized too.
// Result: all 9 RuntimeTypeLoadingTests still pass → both sites untested.
```

## Outputs
- `.bot/plang-types/test-report.json` (shared) — full findings.
- `.bot/plang-types/tester/v4/{plan,result,verdict}.{md,json}`.

## Next
coder adds the two missing sealed-gate tests (renderer + inferred-name) and the `Error.Key`
assert on the sqrt handler test; re-run plang to clear the flaky 502.
