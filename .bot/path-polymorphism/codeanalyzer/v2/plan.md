# codeanalyzer v2 — plan

Re-review of `path-polymorphism` after coder v3 (commit `eb85fcbd`) addressed
the eight findings from `codeanalyzer/v1/report.md`.

## Scope

1. Verify each of F1–F8 is genuinely fixed (not just papered over).
2. Pass 4/5 on the v3-introduced code — the async condition/assert pipeline is
   the largest new surface; trace what breaks silently.
3. Confirm build + both test suites from a clean rebuild (stale-binary trap).

## Method

- Diff `622b619b..eb85fcbd`, read every changed production file.
- For F1: grep the whole tree for `is filepath` / `is httppath` downcasts.
- For F3: trace the async ripple — `AsBooleanAsync` → `Data.ToBooleanAsync`
  → `Operator.Evaluate` → `IEvaluator` → `if`/`elseif`/`compare` → `list.any`
  → `assert`. Check the `IsInitialized` guard and the `== true` path.
- For F4: confirm `InvokeResolve` covers both `As<T>` call sites and catches
  every throw shape from `Resolve`.
- Deletion test on `file.exists` (now a one-liner) and the new guards.
- Behavioral check: did `file.exists` keep the authorization the old
  `ExistsPathAsync` carried?

## Status

Not blocked. Findings written to `v2/report.md`, verdict to `v2/verdict.json`.
