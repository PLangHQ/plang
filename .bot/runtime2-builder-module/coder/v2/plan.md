# Coder v2 Plan — Address Review Feedback

## Issues to Fix

### 1. Goal.MergeFrom — Duplicate step text
**Problem**: Two steps with identical text → both match the first existing step.
**Fix**: Match by index when texts are identical. Walk steps in order, track which existing steps have been consumed. For each fresh step, find the first unconsumed existing step with matching text.

### 2. GoalFile hash — Use module.action
**Problem**: Inline `SHA256.HashData` bypasses the crypto module.
**Fix**: Remove the hash computation from GoalFile entirely. GoalFile produces Goals with `Hash = null`. The caller (builder.goals action or the builder pipeline) is responsible for hashing via `engine.RunAction<crypto.Hash>(...)` if needed. GoalFile is a parser — it shouldn't do crypto.

**Question**: Should the hash be computed in `builder.goals` via RunAction, or left to the builder pipeline? The hash is used to detect step changes between builds. If we compute it in `builder.goals`, every parse includes hashing. If we leave it to the pipeline, GoalFile stays pure.

### 3. File I/O via module.actions
**Problem**: `engine.FileSystem.File.ReadAllText()` and `engine.FileSystem.Directory.GetFiles()` bypass the action pipeline.
**Fix**: Replace with:
- `engine.RunAction<file.Read>(new file.Read { Path = path }, context)` for reading files
- `engine.RunAction<file.List>(new file.List { Path = dir, Pattern = "*.goal", Recursive = true }, context)` for listing
- Same for writes in saveGoals, saveApp, getApp

This requires the actions to be `async` and have access to `Context` (which they already do via `IContext`).

### 4. Don't filter system goals — mark IsSystem
**Problem**: System goals are excluded from results.
**Fix**: Check if path starts with `/system/` and set `IsSystem = true` on the goal. Return all goals.

### 5. Surface JsonException errors
**Problem**: Corrupt .pr files silently swallowed.
**Fix**: Catch `JsonException`, return `Data.FromError()` with the exception message and the file path. User needs to know which file is corrupt and what's wrong.

### 6. Shared JsonSerializerOptions
**Problem**: `new JsonSerializerOptions { ... }` duplicated across files.
**Fix**: Create a static class `PLang.Runtime2.modules.builder.JsonOptions` with:
- `static readonly JsonSerializerOptions CamelCase` — camelCase naming
- `static readonly JsonSerializerOptions CaseInsensitive` — case-insensitive reads
- `static readonly JsonSerializerOptions PrFile` — camelCase, include nulls, indented (for .pr output)

### 7. Rename actions — nouns not verbs
**Problem**: `getActions`, `getGoals`, `getApp`, `getTypeInfo`, `saveApp`, `saveGoals` use verbs.
**Fix**: Rename to:
- `getActions` → `actions`
- `getTypeInfo` → `typeInfo`
- `getGoals` → `goals`
- `validateActions` → `validate`
- `mergeStep` → `merge` (it merges a step — the "step" is the parameter)
- `getApp` → `app`
- `saveApp` → `saveApp` → actually, "save" is a verb too. **Question**: should this be `app` with a "mode" param, or a separate `appSave`? Or keep two actions: `builder.app` (load) and `builder.appSave` (save)?
- `saveGoals` → same question as saveApp

**Proposal**: `builder.app` loads, `builder.appSave` saves. Or: `builder.app` does both depending on whether App param is provided (load vs save). Need Ingi's input on naming the save actions.

### 8. validateActions — Relative path resolution
**Problem**: Scans all `.build/` folders recursively.
**Fix**: Resolve GoalCall paths relative to the goal being built:
- `/somefile.txt` → from engine root
- `somefile.txt` → from the goal's directory
- Compute expected PrPath from the resolved .goal path
- Check if that specific .pr file exists

Need the goal's path passed in (or available on context) so we know the current directory.

### 9. saveGoals — Include nulls in .pr output
**Problem**: `JsonIgnoreCondition.WhenWritingNull` omits nulls.
**Fix**: Remove the `DefaultIgnoreCondition` setting. Use the `PrFile` shared options that include nulls.

## File Changes

| File | Change |
|------|--------|
| `GoalFile.cs` | Remove hash computation |
| `getActions.cs` → `actions.cs` | Rename |
| `getTypeInfo.cs` → `typeInfo.cs` | Rename |
| `getGoals.cs` → `goals.cs` | Rename, use file.Read/file.List, mark IsSystem, surface JsonException |
| `validateActions.cs` → `validate.cs` | Rename, relative path resolution |
| `mergeStep.cs` → `merge.cs` | Rename |
| `getApp.cs` → `app.cs` | Rename, use file.Read |
| `saveApp.cs` | Decide naming, use file.Save |
| `saveGoals.cs` | Decide naming, include nulls |
| New: `JsonOptions.cs` | Shared serialization options |
| `Goal/this.cs` | Fix MergeFrom duplicate text handling |
| All test files | Update for renamed actions |

## Blocking Questions

1. **Hash**: Compute in `builder.goals` via RunAction, or leave to pipeline?
2. **Save action naming**: `builder.appSave` / `builder.goalsSave`? Or overload `builder.app` / `builder.goals` with a save param?
