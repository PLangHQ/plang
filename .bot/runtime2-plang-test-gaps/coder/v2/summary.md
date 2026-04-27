# Coder v2 Summary — Fix Tester Findings + Enforce Goal.Path

## What this is
Addresses tester v1 findings: fixed C# build failure, added missing test coverage, and enforced that all goals must have `Path` set when added to the engine.

## What was done

### 1. Fixed C# build failure (Critical — Finding #1)
`DiscoverAsync` was made private in `Setup/this.cs` but 3 tests still called it. Rewrote the 3 tests to work through `RunAsync` (which calls `DiscoverAsync` internally). Added 2 new convention discovery tests.

### 2. Added PrPath keying tests (Major — Finding #6)
7 new tests in `GoalsTests.cs`:
- Two same-name goals with different PrPaths both accessible
- Get by name with PrPath-keyed storage (linear scan fallback)
- Remove by name when keyed by PrPath
- Same PrPath replaces goal
- Throws when no PrPath

### 3. Enforced Goal.Path requirement (User request)
User asked: "why would there be goal without pr?" → decided to enforce strictly.
- `Goals.Add()` now throws `ArgumentException` if PrPath is null
- Fixed 60+ test sites across 8 test files to set `Path = "/Name.goal"`
- Fixed `Names` property (was returning PrPath keys, now returns goal names)
- Fixed `Get()` variations to search names, not just keys/paths
- Fixed `GetByPrPathAsync` to check `_goals` (PrPath-keyed) before `_byPath`

### 4. Convention discovery tests (Minor — Finding #7)
2 new tests:
- `RunAsync_DiscoversFromSetupSubfolder` — verifies Setup/.build/setup.pr path
- `RunAsync_IgnoresNonConventionPaths` — verifies non-standard locations are skipped

## Files modified

**Production code:**
- `PLang/App/Goals/this.cs` — Enforce PrPath in Add(), fix Names, fix Get() variations, fix GetByPrPathAsync

**Test code (Path enforcement):**
- `PLang.Tests/App/Core/GoalsTests.cs` — All goal creations set Path, 7 new PrPath tests
- `PLang.Tests/App/Goals/Setup/SetupTests.cs` — 3 DiscoverAsync tests → RunAsync, 2 new convention tests, Path added
- `PLang.Tests/App/Core/EngineTests.cs` — Path added to 10 goals
- `PLang.Tests/App/Core/StepRetryTests.cs` — Path added to factory methods
- `PLang.Tests/App/Core/StartGoalTests.cs` — Path added to 6 goals
- `PLang.Tests/App/Modules/loop/ForeachTests.cs` — Path added to 3 goals
- `PLang.Tests/App/Core/StepErrorHandlingTests.cs` — Path added to 2 goals
- `PLang.Tests/App/Modules/condition/ConditionHandlerTests.cs` — Path added to 3 goals
- `Tests/Builder/Start.pr.json` — Added "path" to JSON fixture

## Code example

```csharp
// Goals.Add() enforcement (Goals/this.cs)
public void Add(Goal.@this goal)
{
    goal.Engine = Engine;
    if (string.IsNullOrEmpty(goal.PrPath))
        throw new ArgumentException($"Goal '{goal.Name}' must have a Path set.");
    _goals[goal.PrPath] = goal;
    if (!string.IsNullOrEmpty(goal.Path))
        _byPath[goal.Path] = goal;
}

// Test pattern — all goals now require Path
var goal = new Goal { Name = "TestGoal", Path = "/TestGoal.goal" };
engine.Goals.Add(goal);
```

## Test results
1509/1509 C# tests passing.
