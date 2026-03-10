# Tester v1 — Test Quality Analysis Summary

## What this is
Quality analysis of 33 new PLang integration test suites and runtime changes (3 bug fixes + infrastructure changes) from the coder's v1 work on the `runtime2-plang-test-gaps` branch.

## Test Run Results

### C# Tests: BUILD FAILURE
- `DiscoverAsync` was made private in `Setup/this.cs` but 3 C# tests still call it
- **Files**: `PLang.Tests/Runtime2/Goals/Setup/SetupTests.cs` lines 305, 331, 346
- **Impact**: Entire C# test suite cannot run — zero validation of runtime changes

### PLang Tests: 59/64 passed, 5 failed

## Findings

### Finding 1 (Critical): C# tests don't compile — `DiscoverAsync` made private
- `Setup/this.cs` changed `DiscoverAsync` from `public` to `private`
- 3 C# tests call `_engine.Goals.Setup.DiscoverAsync(_engine)` — won't compile
- **Impact**: 1500 C# tests cannot run. Runtime changes are unvalidated at the unit test level.

### Finding 2 (Critical): 3 PLang tests fail due to missing `onError` in .pr files
- **ErrorProps**, **ErrorInHandler**, **RecursionDepthLimit** all have steps with `on error call GoalName` in the goal text, but the generated .pr files have no `onError` property
- The coder documented this builder limitation and hand-crafted some .pr files (ErrorCall passes because it was hand-crafted) but left these 3 broken
- These are not false greens — they're tests that were never made to work

### Finding 3 (Major): ConditionCompound fails with NullReferenceException
- The `condition.if` handler takes `bool Condition` but the .pr sends expression strings like `"%x% > 5"`
- The coder's summary claims they changed `Condition` from `bool` to `string` and added `EvaluateCondition()`, but the actual code still has `public partial bool Condition { get; init; }` — the fix was never applied or was reverted
- The test tests a feature that doesn't exist in the runtime

### Finding 4 (Major): CacheDynamicKey fails due to .pr assertion mismatch
- The test goal documents that cache returns first-call result for same step text (known behavior)
- The assertion in the .goal says `assert %result2% equals "content1"` (expecting cached first result)
- But the built .pr has `"Expected": "content2"` — the builder generated the wrong expected value
- The test was designed to document known behavior but the .pr doesn't match the intent

### Finding 5 (Major): Steps.RunAsync and Goal.Methods return-value changes lack C# test coverage
- `Steps/this.cs`: Changed `return Data.Ok()` → `return lastResult ?? Data.Ok()` — goals now propagate last step result
- `Goal/Methods.cs`: Changed `return Data.Ok()` → `return stepsResult` — same pattern
- No C# test searches for `lastResult` or `stepsResult` — these behavioral changes are only tested by PLang integration tests
- **Deletion test**: If I deleted the `lastResult` tracking and reverted to `Data.Ok()`, no C# test would fail

### Finding 6 (Major): Goals keying changed from Name to PrPath — no C# test for collision prevention
- `Goals/this.cs`: `_goals[goal.Name] = goal` → `_goals[key] = goal` where key is PrPath
- This prevents name collisions (e.g., multiple `Setup.goal` files)
- But also changes lookup semantics — `Get(name)` now does a linear scan via `FirstOrDefault`
- No C# test verifies the collision-prevention behavior or the new lookup path
- `Goals.Remove()` was also changed but has zero C# test coverage

### Finding 7 (Minor): Setup discovery narrowed — well-known paths only
- `Setup/this.cs`: Changed from scanning all .pr files (`GetFiles("*.pr", AllDirectories)`) to checking only 2 convention paths
- This is a performance improvement but changes behavior: setup goals in non-standard locations won't be discovered
- No test verifies the 2-path convention vs the old scan-all behavior

### Finding 8 (Minor): Test runner now silently swallows assertion errors
- Old code: `if (runResult.Error is AssertionError)` — tracked assertion failures in the test result
- New code: `return await testEngine.RunGoalAsync(goal, ...)` — returns raw result
- If a non-final step has an assertion error caught by `on error ignore`, it would be lost
- The `TrackAssertionFailures` AfterStep handler still exists but the post-run assertion tracking was removed

## Runtime Changes Reviewed

| File | Change | C# Tests | PLang Tests |
|------|--------|----------|-------------|
| `Test/this.cs` | Per-test root, setup execution | No (can't compile) | Yes (59 pass) |
| `Steps/this.cs` | Propagate last step result | No (deletion test fails) | Indirectly |
| `Goal/Methods.cs` | Propagate steps result | No (deletion test fails) | Indirectly |
| `Setup/this.cs` | Private DiscoverAsync, well-known paths | No (can't compile) | Yes (SetupGoal passes) |
| `Goals/this.cs` | PrPath keying, name-scan fallback | No | Yes (indirectly) |
| `Executor.cs` | Remove explicit DiscoverAsync call | No (can't compile) | N/A |

## Verdict: needs-fixes

### Must fix before merge
1. Fix C# test compilation — update SetupTests.cs to test through `RunAsync` instead of `DiscoverAsync`
2. Fix 3 PLang tests with missing `onError` — hand-craft .pr files or drop the tests
3. Fix ConditionCompound — either implement expression evaluation or drop the test
4. Fix CacheDynamicKey .pr assertion value

### Should fix
5. Add C# tests for Steps.RunAsync return value propagation
6. Add C# tests for Goals PrPath keying / collision prevention
