# v1 Summary — Builder Module Implementation

## What this is

The builder module (`PLang.Runtime2.modules.builder`) provides native Runtime2 actions that the PLang builder uses to parse `.goal` files, validate LLM output, and save `.pr` files. It replaces the v1 `PlangModule/Program.cs` bridge functions with zero Runtime1 dependencies. When the builder migrates to Runtime2, these actions become direct `builder.*` calls.

## What was done

### Entity Methods (Phase 1)
- **`Step.Merge(Step from)`** — Added to `PLang/Runtime2/Engine/Goals/Goal/Steps/Step/this.cs:100`. Copies LLM-derived fields (Actions, Cache, OnError, Errors, Warnings) while preserving structural fields (Text, Index, Indent, LineNumber). Only replaces Errors/Warnings when source has entries.
- **`Goal.MergeFrom(Goal existing)`** — Added to `PLang/Runtime2/Engine/Goals/Goal/this.cs:140`. Matches steps by `Text`, delegates to `Step.Merge` for each match. Null/empty existing is a no-op.

### GoalFile (Phase 2)
- **`PLang/Runtime2/modules/builder/GoalFile.cs`** — Instance class that IS the .goal file format. Parses .goal text into `List<Goal>` with Steps. Handles: blank lines, `/` line comments, `/* */` block comments, `- ` step lines, continuation lines, indentation (4 spaces = 1 level), tabs→spaces, goal headers (first=Public, rest=Private), SubGoals list, LineNumber tracking, SHA256 hash.

### Builder Actions (Phase 3) — 8 actions
All follow `[Action] partial class : IContext` pattern with `Building.IsEnabled` guard.

| File | Action | Purpose |
|------|--------|---------|
| `getActions.cs` | `builder.getActions` | Reflects engine.Modules into parameter schema metadata for LLM prompt |
| `getTypeInfo.cs` | `builder.getTypeInfo` | Returns type names + complex type schemas via TypeMapping |
| `getGoals.cs` | `builder.getGoals` | Finds .goal files, parses via GoalFile, filters system goals, merges .pr data |
| `validateActions.cs` | `builder.validateActions` | Checks actions exist, resolves GoalCall PrPaths, fills defaults |
| `mergeStep.cs` | `builder.mergeStep` | Thin delegation to Step.Merge() |
| `getApp.cs` | `builder.getApp` | Loads or creates .build/app.pr |
| `saveApp.cs` | `builder.saveApp` | Saves AppData with updated timestamp |
| `saveGoals.cs` | `builder.saveGoals` | Serializes List<Goal> to .pr file (camelCase, nulls omitted) |

### Tests (Phase 4)
- **53 C# tests** implemented across 10 test files — all pass
- **GoalFileTests** (13): parser edge cases
- **MergeTests** (7): Step.Merge + Goal.MergeFrom
- **GetActionsTests** (5): module reflection, nullable markers, @var, defaults, cacheable
- **GetTypeInfoTests** (2): type names + complex schemas
- **GetGoalsTests** (5): parse, exclude system, merge .pr, empty folder, corrupt .pr
- **ValidateActionsTests** (5): valid/unknown actions, GoalCall resolution, dynamic names, defaults
- **MergeStepTests** (1): delegation
- **AppTests** (3): load existing, create new, save with timestamp
- **SaveGoalsTests** (3): serialize, camelCase, multi-goal
- **BuildingGuardTests** (8): one per action

### Full test suite: 2015 tests, 0 failures.

## Code example

Builder action pattern (all 8 follow this):
```csharp
[Action("getTypeInfo")]
public partial class getTypeInfo : IContext
{
    public Task<Data> Run()
    {
        var engine = Context.Engine;
        if (!engine.Building.IsEnabled)
            return Task.FromResult(Data.FromError(
                new Engine.Errors.ActionError("Building is not enabled", "BuildingDisabled", 400)));

        var names = TypeMapping.GetBuilderTypeNames();
        // ...
        return Task.FromResult(Data.Ok(result));
    }
}
```

## What's next
- **Deferred**: Updating `system/builder/*.goal` files from `[plang]` to `builder.*` — blocked until builder migrates to Runtime2 runtime.
- Run **codeanalyzer** for OBP compliance review.
