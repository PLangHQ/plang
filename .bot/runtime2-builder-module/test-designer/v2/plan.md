# v2 Plan — Address Gaps in Builder Test Stubs

## Changes

### 1. GoalFileTests.cs — Add 2 tests
- `Parse_StepLineNumbers_MatchSourceLines` — Verify Step.LineNumber is 1-based line position
- `Parse_PrPath_DerivedFromPath` — Verify /folder/MyGoal.goal → /folder/.build/mygoal.pr derivation

### 2. GetGoalsTests.cs — Add 1 test
- `GetGoals_CorruptPrFile_IgnoresAndReparses` — Malformed .pr JSON shouldn't crash; treat as no existing data

### 3. BuildingGuardTests.cs — Expand from 1 to 8 tests
Replace the single vague test with one per action:
- `GetGoals_BuildingDisabled_ReturnsError`
- `GetActions_BuildingDisabled_ReturnsError`
- `ValidateActions_BuildingDisabled_ReturnsError`
- `SaveGoals_BuildingDisabled_ReturnsError`
- `GetApp_BuildingDisabled_ReturnsError`
- `SaveApp_BuildingDisabled_ReturnsError`
- `MergeStep_BuildingDisabled_ReturnsError`
- `GetTypeInfo_BuildingDisabled_ReturnsError`

### 4. PLang test stubs — Flesh out with concrete steps
- GetActions: `get all actions, write to %actions%` + assert non-empty
- GetTypeInfo: `get type info, write to %typeInfo%` + assert non-empty
- ParseGoal: save test .goal file, `get goals from path`, assert non-empty
- ValidateValid: get actions then validate, assert true
- MergeStep & ValidateInvalid: mark as deferred to C# (requires object construction)

## Totals after v2
- **C# tests:** 43 → 53 (+10: 2 GoalFile, 1 GetGoals, 7 net new BuildingGuard)
- **PLang tests:** 6 (same count, but 4 now have concrete steps)
- **Total: 59**
