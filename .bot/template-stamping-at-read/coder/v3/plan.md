# v3 — Migrate PLang.Tests/Data executor tests to the real read path

Copy the landed `Make` + `RealGoalLoad.ViaChannel` + `TestApp.Create` pattern onto
executor tests in PLang.Tests/Data. No new design. (v2 was a sibling Modules-migration
task; this is the Data-project scope.)

## Rule (from canonical examples)
A test that hand-builds a Goal/Step/PrAction AND executes it (`RunGoalAsync` /
`steps.RunAsync`) for a behavioral assertion → load via `RealGoalLoad.ViaChannel(app,
Make.Goal(...))`. Swap `new app.@this` → `TestApp.Create` in migrated tests.

Precedent (PlangRuntimeTests): even in migrated files, `steps.RunAsync` kernel/step-
machinery tests stay hand-built — only full goal-execution tests where born-typing
matters get migrated.

## Per-file decisions
- **EngineTests.cs** — MIGRATE 3 `RunGoalAsync` behavioral tests
  (`RunGoalAsync_ExecutesSteps`, `_WithActor_UsesActorContext`,
  `_ByName_WithActor_UsesActorContext`). LEAVE: constructor/dispose/actor tests
  (no goal exec); `steps.RunAsync` machinery tests; `_CancelledToken` (step never runs);
  `_StepFailure` (deliberately-malformed variable.get); `_EmptyGoal` (no steps). Keep
  `MakeStep*` helpers (still used by left-alone tests).
- **StartGoalTests.cs** — MIGRATE `StartGoal_Programmatic...` + the five `ResolveValue_*`
  tests. LEAVE the three `Defaults_*` tests — Make has no `Defaults` seam.
- **PrPipelineTests.cs** — LEAVE ALL. Fixtures load real `.pr` off disk (already real
  path); hand-built ones depend on goal `Path` at subfolders which `Make.Goal` hardcodes
  to root.
- **GoalTests.cs / StepTests.cs / VariablesTests.cs** — LEAVE ALL (structural / Variables-
  collection unit tests; no goal execution).

## Verify
`dotnet build PLang.Tests/Data` clean + run: only pre-existing
`Diff_DiffModeOverLargeListDoesNotOom` red. New red on a migrated file → revert + report.
