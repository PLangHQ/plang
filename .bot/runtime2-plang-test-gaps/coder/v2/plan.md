# Coder v2 Plan — Fix Tester Findings

## Goal
Fix the critical C# build failure and add missing C# test coverage for runtime changes.

## Tasks

### 1. Fix SetupTests.cs compilation (Critical — Finding #1)
`DiscoverAsync` is now private. Three tests call it directly:
- `DiscoverAsync_OnlyLoadsSetupGoals` (line 305)
- `DiscoverAsync_NonSetupGoalsRemainLazyLoadable` (line 331)
- `DiscoverAsync_HandlesEmptyDirectory` (line 346)

**Fix**: Rewrite tests to call `RunAsync` instead (which calls `DiscoverAsync` internally). The setup goals have empty `steps:[]` so RunAsync succeeds. Rename test methods to reflect they test through RunAsync.

### 2. Add Goals PrPath keying tests (Major — Finding #6)
New tests in `SetupTests.cs` or a new file:
- Two goals with same Name but different PrPath — both accessible
- Get by name when keyed by PrPath (linear scan fallback)
- Remove by name when keyed by PrPath

### 3. Add Setup convention discovery tests (Minor — Finding #7)
- Verify discovery from both convention paths (.build/setup.pr and Setup/.build/setup.pr)
- Verify non-standard location setup.pr is NOT discovered

### 4. Add Steps return value propagation test (Major — Finding #5)
Test that Steps.RunAsync returns the last step's result, not a fresh Data.Ok().

### Not in scope
- Findings #2, #3, #4: builder issues / deferred to todos
- Finding #8: acceptable tradeoff, no code change needed

## Files to modify
- `PLang.Tests/App/Goals/Setup/SetupTests.cs` — fix 3 tests, add new tests
- `PLang.Tests/App/Goals/GoalsTests.cs` — new file for PrPath keying tests (or add to SetupTests)
