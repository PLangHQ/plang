# Coder v1 Plan — Builder Module Implementation

## Overview

Implement the `App.modules.builder` module: 8 actions + `GoalFile` class + `Step.Merge()` + `Goal.MergeFrom()`. Zero Runtime1 dependencies. All file I/O through `engine.RunAction` with the `file` module. Building guard on every action.

## Implementation Order

### Phase 1: Entity Methods (Step.Merge, Goal.MergeFrom)
These are prerequisites for the actions.

**1. `Step.Merge(Step from)`** — Add to `PLang/App/Engine/Goals/Goal/Steps/Step/this.cs`
- Copies LLM-derived fields: Actions, Cache, OnError
- Preserves structural fields: Text, Index, Indent, LineNumber
- Replaces Errors/Warnings only when source has entries
- Reference: v1 `MergeStep` in Program.cs lines 382-414

**2. `Goal.MergeFrom(Goal existing)`** — Add to `PLang/App/Engine/Goals/Goal/this.cs`
- Matches steps by `Step.Text` (case-sensitive)
- Delegates to `Step.Merge(existingStep)` for each match
- Unmatched steps keep empty Actions
- Null/empty existing → no-op

### Phase 2: GoalFile
**3. `GoalFile.cs`** — New file `PLang/App/modules/builder/GoalFile.cs`
- Instance class, not static — a GoalFile IS the file format
- `Parse(string text, string path)` → `List<Goal>`
- Rules: blank lines skip, `/` = comments, `- ` = steps, continuation lines, goal headers
- Tabs → 4 spaces
- `/* */` multi-line comments
- First goal = Public, rest = Private
- SubGoals list on public goal
- Path set on all goals, PrPath auto-derives
- LineNumber tracking (1-based)
- Hash computation (SHA256 of step text)

### Phase 3: Builder Actions (8 actions)
All follow the standard `[Action] partial class : IContext` pattern. All check `Context.Engine.Building.IsEnabled` first.

**4. `getActions.cs`** — Navigate `engine.Modules`, reflect properties, build parameter metadata
- Same logic as v1 `GetActions()` in Program.cs lines 64-137
- Returns `Actions` collection (the `Steps.Step.Actions.@this` list type)
- Parameter metadata: type name, nullable, @var, default, valid values

**5. `getTypeInfo.cs`** — Delegate to `TypeMapping.GetBuilderTypeNames()` and `GetComplexTypeSchemas()`
- Same as v1 `GetTypeInfo()` in Program.cs lines 189-197

**6. `getGoals.cs`** — Find .goal files, parse via GoalFile, exclude system goals, merge .pr data
- File I/O: use `engine.FileSystem` directly (consistent with how getGoals actually works — reading .goal text, reading .pr JSON)
- Parse via `GoalFile.Parse()`
- Filter out goals where path starts with `/system/`
- Merge: load `List<Goal>` from PrPath, match by Name, delegate to `goal.MergeFrom()`

**7. `validateActions.cs`** — Check actions exist in `engine.Modules`, resolve GoalCall paths, fill defaults
- Same logic as v1 `ValidateActions()` in Program.cs lines 200-228
- GoalCall resolution: scan `.build/` for `.pr` files, fallback to `.goal` files
- Dynamic names (containing %) skipped
- Fill defaults from `[Default]` attributes or `IConfigure<T>`

**8. `mergeStep.cs`** — Thin delegation to `Step.Merge(StepFromLlm)`
- Takes Step and StepFromLlm, calls `Step.Merge(StepFromLlm)`, returns `Data.Ok(Step)`

**9. `getApp.cs`** — Load or create `.build/app.pr`
- Read file, deserialize as AppData
- If missing, create new with GUID and Version = "0.2"

**10. `saveApp.cs`** — Save AppData to disk
- Update `App.Updated` timestamp
- Serialize and write

**11. `saveGoals.cs`** — Serialize goals to `.pr` file
- PrPath from first goal
- Serialize `List<Goal>`, camelCase, nulls omitted
- Write via fileSystem

### Phase 4: Tests
**12. Implement all 53 C# test stubs** — Make them pass against the new code
**13. PLang tests** — The 4 concrete PLang tests (deferred for now since builder itself runs on v1)

### Phase 5: File I/O Decision

The architect plan says "file I/O goes through engine.RunAction". But the v1 code in `GetGoalsV2` uses `fileSystem` directly. Since the builder module actions need to:
- Read .goal file text (string)
- Read .pr file JSON (string)
- Write .pr file JSON (string)
- List .goal files in a directory

I'll use `engine.FileSystem` directly for reads and `engine.RunAction<file.Save>` for writes. This is the pragmatic approach — file reads are simple string I/O, but writes benefit from the action pipeline (events, error handling). For listing files, I'll use `engine.FileSystem.Directory.GetFiles()`.

**Note**: The architect plan mentions updating system/builder/*.goal files to use `builder.*` instead of `[plang]`. This is deferred — the builder runs on v1 runtime and can't call App actions until migration. The module is being built so it's ready when that migration happens.

## Files Created
| File | Purpose |
|------|---------|
| `PLang/App/modules/builder/GoalFile.cs` | .goal file format parser |
| `PLang/App/modules/builder/getActions.cs` | Action registry metadata |
| `PLang/App/modules/builder/getTypeInfo.cs` | Type names + schemas |
| `PLang/App/modules/builder/getGoals.cs` | Find + parse + merge .goal files |
| `PLang/App/modules/builder/validateActions.cs` | Validate + resolve + defaults |
| `PLang/App/modules/builder/mergeStep.cs` | Step merge delegation |
| `PLang/App/modules/builder/getApp.cs` | Load/create app.pr |
| `PLang/App/modules/builder/saveApp.cs` | Save app.pr |
| `PLang/App/modules/builder/saveGoals.cs` | Save goals as .pr |

## Files Modified
| File | Change |
|------|--------|
| `PLang/App/Engine/Goals/Goal/this.cs` | Add `MergeFrom(Goal existing)` |
| `PLang/App/Engine/Goals/Goal/Steps/Step/this.cs` | Add `Merge(Step from)` |
| `PLang.Tests/App/Modules/builder/*.cs` | Implement all test stubs |

## Key Decisions
1. **No engine.RunAction for reads** — Direct fileSystem access for reading .goal and .pr files. Simpler, faster, and the v1 code does the same.
2. **Building guard via helper method** — Static method `BuildingGuard(IContext action)` to avoid repeating the check in every action.
3. **GoalFile is an instance** — `new GoalFile().Parse(text, path)` — the instance IS the file, holding parse state.
4. **Hash = SHA256 of goal text** — Consistent with existing pattern for detecting changes.
