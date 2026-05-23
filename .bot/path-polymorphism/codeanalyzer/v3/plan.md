# codeanalyzer v3 — plan

Re-review of `path-polymorphism` after coder addressed the three v2 findings
(N1–N3) in commit `a1c3f9563`.

## Scope

Narrow — three targeted fixes across three production files:

- `PLang/app/types/path/file/this.Operations.cs` — N1
- `PLang/app/types/path/this.cs` — N2
- `PLang/app/modules/assert/code/Default.cs` — N3

Plus two test files: `DefaultEvaluatorTests.cs`, `HandlerShapeTests.cs`.

## Method

1. **N1** — confirm `FilePath.AsBooleanAsync` now routes through the gated
   `ExistsAsync`; trace the denied-probe path (gate fails → `Success=false` →
   returns `false`); confirm parity with `HttpPath.AsBooleanAsync`.
2. **N2** — confirm `Equals`/`GetHashCode` use `RootComparison`; verify
   `RootComparison` is a `StringComparison` and `StringComparer.FromComparison`
   is a valid call.
3. **N3** — confirm `ResolveTruthy` delegates the resolvable branch to
   `Data.ToBooleanAsync()`; check for behavioral divergence via the
   `IsInitialized` guard in `ToBooleanAsync`.
4. Pass 4/5 on the three fixes; deletion test on the added lines.
5. Clean rebuild + both test suites (stale-binary trap).

## Status

Not blocked. Findings → `v3/report.md`, verdict → `v3/verdict.json`.
