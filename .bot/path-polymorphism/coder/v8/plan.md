# v8 plan — address tester v7 NEEDS-FIXES

5 findings:

- **F4-CARRY** (major) — `.test.goal2` parked file. Rename to `.test.goal`,
  delete stale `.pr`, build just that fixture + its two callees.
- **N1** (major) — `GoalCall.GetGoalAsync` slash-qualified resolution has no
  unit tests. Write `PLang.Tests/App/Goals/GoalCallResolutionTests.cs` with
  the four canonical cases: caller-ancestor walk, root-relative fallback,
  bare-name regression guard, LoadFromFile leaf-match.
- **N2** (major) — `builder.actions`'s new `Actions` filter param has zero
  test coverage. Append 4 tests to `GetActionsTests.cs`: restrict-to-named,
  empty-list = no-filter, unknown name → empty, case-insensitive.
- **N3** (minor) — inverted `File.Exists` in `Builder.RunAsync` has no
  regression guard. New `BuilderRunAsyncTests.cs` with the two-test pair:
  missing app.pr + headless stdin → NoAppFound; existing app.pr →
  Error.Key ≠ "NoAppFound".
- **N4** (minor) — `Action.ReturnTypeName` zero coverage. New
  `ModulesDescribeReturnTypeTests.cs` over a representative slice
  (bare Data → "data", Data<bool> → "bool", Data<path> → "path",
  Data<List<path>> → "list<path>", Data<Identity>, Data<List<Identity>>,
  and a sanity "every catalog row carries a value" sweep).

N5 (baseline-tests.md) — addressed by writing the file this version.

No production-code edits. Tests-only pass.
