# Baseline — v2 (Data project test migration)

Scope: PLang.Tests/Data only (task is scoped to this project).

## Build
`dotnet build PLang.Tests/Data -p:RunAnalyzers=false` — clean, zero `error CS`.

## PLang.Tests.Data run
- failed: 1
- Failing test: `Diff_DiffModeOverLargeListDoesNotOom` (CallStack DiffCapture perf/OOM
  assertion — "Expected to be less than 50"). Pre-existing, unrelated to goal-load
  migration. Flaky/perf — not my regression.

Any test green here that goes red after migration = my regression.
