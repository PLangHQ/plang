# Coder v5 — plan

Responds to codeanalyzer v2 (`NEEDS WORK`). See `codeanalyzer-v2_review_summary.md`.

## Baseline (before v5)

C# `dotnet run --project PLang.Tests` — 2881 / 2881 pass.
PLang `cd Tests && plang --test` — 203 total / 203 pass / 0 fail / 0 stale.
Build clean (0 errors). Captured at branch HEAD `0f224e314`.

## N1 — restore the `file.exists` authorization gate  *(Medium)*

`FilePath.AsBooleanAsync()` skips `AuthGate`. Fix: route it through the already
-gated `ExistsAsync()`, exactly as `HttpPath.AsBooleanAsync()` does — both
schemes then share one shape (probe via `ExistsAsync`; a denied/errored probe
answers `false`). Removes the asymmetry *and* the duplicated
`File.Exists || Directory.Exists` body.

File: `PLang/app/types/path/file/this.Operations.cs`.

## N2 — `path.Equals` / `GetHashCode` → `RootComparison`  *(Low)*

Both currently hard-code `OrdinalIgnoreCase`. Switch to `RootComparison`
(`Ordinal` on Linux, `OrdinalIgnoreCase` on Windows). `GetHashCode` uses
`StringComparer.FromComparison(RootComparison)`.

File: `PLang/app/types/path/this.cs`.

## N3 — dedup `assert.ResolveTruthy`'s resolvable dispatch  *(Low)*

`ResolveTruthy` calls `IBooleanResolvable.AsBooleanAsync()` directly, duplicating
`Data.ToBooleanAsync()`'s dispatch. Fix: for an `IBooleanResolvable` value,
delegate to `data.ToBooleanAsync()`; keep `IsTruthy` for plain values (assert's
string-`"false"` semantics differ from `Data.ToBoolean` — not collapsing those).

File: `PLang/app/modules/assert/code/Default.cs`.

## Verification

Clean rebuild, both suites, plus the F3/path tests the review named
(`IfExists_PathToMissingFile_IsFalse`, per-scheme `AsBooleanAsync`,
`PathAbstractTests`). Add a C# test for the N1 gate restoration.
