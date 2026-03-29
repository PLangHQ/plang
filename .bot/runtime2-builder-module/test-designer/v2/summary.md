# v2 Summary — Address Gaps in Builder Test Stubs

## What this is

Gap-fill pass on the v1 builder module test suite. Adds coverage for line number tracking, PrPath derivation, corrupt .pr resilience, per-action building guards, and concrete PLang test steps.

## What was done

### C# tests added (10 new stubs)

**GoalFileTests.cs** (+2):
- `Parse_StepLineNumbers_MatchSourceLines` — Line numbers are 1-based from source
- `Parse_PrPath_DerivedFromPath` — /folder/MyGoal.goal → /folder/.build/mygoal.pr

**GetGoalsTests.cs** (+1):
- `GetGoals_CorruptPrFile_IgnoresAndReparses` — Malformed .pr JSON doesn't crash

**BuildingGuardTests.cs** (+7 net, replaced 1 vague test with 8 specific):
- One test per builder action verifying Data.FromError when building disabled

### PLang tests fleshed out

4 of 6 PLang tests now have concrete step shapes matching existing test patterns:
- `BuilderGetActions.test.goal` — `get all actions, write to %actions%` + assert
- `BuilderGetTypeInfo.test.goal` — `get type info, write to %typeInfo%` + assert
- `BuilderParseGoal.test.goal` — save test file, `get goals from path` + assert
- `BuilderValidateValid.test.goal` — get actions → validate → assert true

2 remain deferred (MergeStep, ValidateInvalid) — they need object construction not available in PLang syntax.

## Code example

Building guard pattern (all 8 follow this):
```csharp
[Test]
public async Task GetGoals_BuildingDisabled_ReturnsError()
{
    // builder.getGoals with engine.Building.IsEnabled=false → Data.FromError
    Assert.Fail("Not implemented");
}
```

PLang test pattern (concrete steps):
```plang
Start
/ Test that builder.getActions returns a list of all registered module actions
- get all actions, write to %actions%
- assert %actions% is not empty, "getActions should return registered module actions"
```

## Totals
- **C# tests:** 53 (was 43)
- **PLang tests:** 6 (4 with concrete steps, 2 deferred)
- **Total: 59**
