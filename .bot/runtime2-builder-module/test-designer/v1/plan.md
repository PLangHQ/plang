# Test Plan: Piece 8 — Builder Module

## Overview

The builder module (`App.modules.builder`) is a native v2 builder with zero Runtime1 dependencies. It parses `.goal` files into App Goal/Step types, manages `.pr` files, reflects action metadata for the LLM, and validates LLM output. This test suite defines the behavioral contract.

## Test Areas & Batches

### Batch 1: GoalFile (~12 C# tests)
The core parser. This is the one place PLang actually parses text (everything else is LLM-mapped), so it needs thorough coverage.

| # | Test | What it verifies |
|---|------|-----------------|
| 1 | `Parse_SingleGoalWithSteps_ReturnsOneGoal` | Basic happy path — goal name, steps extracted |
| 2 | `Parse_MultipleGoals_FirstPublicRestPrivate` | Visibility rules: first = Public, rest = Private |
| 3 | `Parse_IndentedSteps_SetsIndentLevel` | 4 spaces = indent 1, 8 spaces = indent 2 |
| 4 | `Parse_ContinuationLines_AppendsToStepText` | Indented non-dash lines join with `\n` |
| 5 | `Parse_GoalComments_SetsGoalComment` | `/` lines before first step → goal comment |
| 6 | `Parse_StepComments_SetsStepComment` | `/` lines between steps → next step's comment |
| 7 | `Parse_MultiLineComments_HandledCorrectly` | `/* ... */` block comments |
| 8 | `Parse_PathComputation_AllGoalsSharePath` | All goals get same Path, PrPath derives correctly |
| 9 | `Parse_EmptyFile_ReturnsEmptyList` | Edge: empty string → empty list |
| 10 | `Parse_TabsConvertedToSpaces` | Tabs → 4 spaces before parsing |
| 11 | `Parse_SubGoalNames_PopulatedOnPublicGoal` | SubGoals list on first goal contains names of non-first goals |
| 12 | `Parse_BlankLinesBetweenGoals_HandledCorrectly` | Blank lines don't create phantom goals |

**My additions beyond architect:** #10 (tabs), #12 (blank lines as boundary edge case) — the parser is line-based and these are classic off-by-one traps.

### Batch 2: Step.Merge & Goal.MergeFrom (~7 C# tests)
These are OBP methods on the entities themselves.

| # | Test | What it verifies |
|---|------|-----------------|
| 13 | `StepMerge_CopiesLlmDerivedFields` | Actions, Cache, OnError copied from source |
| 14 | `StepMerge_PreservesStructuralFields` | Text, Index, Indent, LineNumber untouched |
| 15 | `StepMerge_EmptySource_LeavesTargetUnchanged` | No-op when source has no LLM fields |
| 16 | `StepMerge_ErrorsReplacedOnlyWhenSourceHasEntries` | Errors/Warnings replaced only when source has them |
| 17 | `GoalMergeFrom_MatchesByText_MergesLlmFields` | Steps matched by Text, Step.Merge called |
| 18 | `GoalMergeFrom_UnmatchedSteps_KeepEmptyActions` | New steps not in existing keep empty Actions |
| 19 | `GoalMergeFrom_NullExisting_NoOp` | Null or empty existing → no crash |

**My addition:** #16 — the "replace only when source has entries" semantic is subtle and easy to get wrong.

### Batch 3: getActions & getTypeInfo (~7 C# tests)
Reflection-based metadata for the LLM prompt.

| # | Test | What it verifies |
|---|------|-----------------|
| 20 | `GetActions_ReturnsAllModulesAndActions` | Iterates engine.Modules, returns entries |
| 21 | `GetActions_ParameterTypes_IncludeNullableMarkers` | Nullable properties get `?` suffix |
| 22 | `GetActions_VariableNameParams_Marked` | `[VariableName]` properties flagged |
| 23 | `GetActions_DefaultValues_Included` | `[Default]` attribute values surfaced |
| 24 | `GetActions_CacheableFlag_FromActionAttribute` | `[Action(Cacheable = false)]` reflected |
| 25 | `GetTypeInfo_ReturnsBuilderTypeNames` | Delegates to TypeMapping.GetBuilderTypeNames() |
| 26 | `GetTypeInfo_ReturnsComplexTypeSchemas` | Delegates to TypeMapping.GetComplexTypeSchemas() |

**My addition:** #24 — Cacheable defaults to true, so testing explicit false is important.

### Batch 4: getGoals & validateActions (~9 C# tests)
The core build pipeline actions.

| # | Test | What it verifies |
|---|------|-----------------|
| 27 | `GetGoals_ParsesGoalFilesFromFolder` | Finds .goal files, parses via GoalFile |
| 28 | `GetGoals_ExcludesSystemGoals` | Paths starting with /system/ filtered out |
| 29 | `GetGoals_MergesExistingPrData` | Loads existing .pr, matches by Name, merges |
| 30 | `GetGoals_EmptyFolder_ReturnsEmptyList` | No .goal files → empty list |
| 31 | `ValidateActions_ValidActions_ReturnsOk` | All actions found in engine.Modules → success |
| 32 | `ValidateActions_UnknownAction_ReturnsError` | Action not in registry → error with name |
| 33 | `ValidateActions_GoalCallPath_Resolved` | GoalCall PrPath resolved via .build/ scan |
| 34 | `ValidateActions_DynamicNames_Skipped` | Names containing `%` not resolved |
| 35 | `ValidateActions_DefaultsFilled` | `[Default]` values applied to missing params |

**My addition:** #30 — empty folder is a real scenario (new project, no .goal files yet).

### Batch 5: getApp, saveApp, saveGoals, mergeStep (~8 C# tests)

| # | Test | What it verifies |
|---|------|-----------------|
| 36 | `GetApp_LoadsExistingAppPr` | Reads and deserializes existing app.pr |
| 37 | `GetApp_CreatesNewWhenMissing` | New AppData with GUID and Version "0.2" |
| 38 | `SaveApp_UpdatesTimestamp` | App.Updated set before write |
| 39 | `SaveGoals_SerializesToPrPath` | Writes List<Goal> as JSON to derived PrPath |
| 40 | `SaveGoals_CamelCase_NullsOmitted` | JSON uses camelCase, nulls excluded |
| 41 | `SaveGoals_MultipleGoals_SingleFile` | Multiple goals from one .goal → one .pr file |
| 42 | `MergeStep_DelegatesToStepMerge` | Thin wrapper — calls Step.Merge, returns step |
| 43 | `BuilderActions_BuildingDisabled_ReturnsError` | engine.Building.IsEnabled=false → error |

**My addition:** #43 — the Building guard is a cross-cutting concern on all builder actions.

### Batch 6: PLang Integration Tests (~6 PLang tests)

| # | Test | What it verifies |
|---|------|-----------------|
| P1 | `BuilderGetActions/` | PLang step calls builder.getActions, gets module list |
| P2 | `BuilderGetTypeInfo/` | PLang step calls builder.getTypeInfo, gets type names |
| P3 | `BuilderParseGoal/` | PLang step calls builder.getGoals on a test .goal file |
| P4 | `BuilderMergeStep/` | PLang step calls builder.mergeStep with two steps |
| P5 | `BuilderValidateValid/` | PLang step validates known actions → success |
| P6 | `BuilderValidateInvalid/` | PLang step validates unknown action → error |

## Totals

- **C# tests:** 43 stubs
- **PLang tests:** 6 stubs
- **Total: 49**

## File Locations

- C# tests: `PLang.Tests/App/Modules/builder/`
- PLang tests: `Tests/App/Builder/`
