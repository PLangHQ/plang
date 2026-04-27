# Piece 8: Builder Module

> **Note for coder**: This module lives in `App.modules.builder` and follows the standard action pattern (`partial class`, `[Action]`, `IContext`, source-generated lazy params). Zero Runtime1 dependencies. Read existing modules (`file`, `llm`, `output`) as reference for patterns.

## Decision Log

- **No Runtime1 dependencies.** No `IGoalParser`, no `PrParser`, no `GoalMapper`, no `Building.Model`. The builder module parses `.goal` files directly into App Goal/Step types and reads `.pr` files as App JSON.
- **Native `.goal` parser.** A `GoalFile` instance in the builder module parses `.goal` text into App `Goal` and `Step` objects. Line-by-line parser — no Sprache dependency, no v1 model intermediary.
- **`.pr` files are multi-goal App JSON.** One `.goal` file → one `.pr` file containing `List<Goal>`. Reading is `Deserialize<List<Goal>>(json)`. All goals from one `.goal` file share the same `Path` → same derived `PrPath` → one `.pr` file.
- **No provider pattern.** The builder module has no swappable behavior.
- **File I/O goes through `engine.RunAction`.** Same pattern as the LLM module using `http.request`.
- **Navigate `engine.Modules`, don't construct.** The engine already owns the module registry. Actions navigate `Context.Engine.Modules` — never `new EngineModules()`.
- **Step owns its merge.** Add `Step.Merge(Step from)` — Step knows its own mutable fields. The `mergeStep` action delegates to it.
- **Goal owns its merge.** Add `Goal.MergeFrom(Goal existing)` — Goal matches steps by Text and delegates to `Step.Merge`. The `getGoals` action calls it, doesn't contain matching logic itself.
- **Engine.Building guard.** `engine.Building` is always non-null. Guard on `engine.Building.IsEnabled` — if false, return `Data.FromError(...)`.
- **GoalCall path resolution uses the file system directly.** Scans `.build/` for `.pr` files, or source tree for `.goal` files, to find matching goals. No PrParser.
- **v0.2 `.pr` format.** Single `.pr` file per `.goal` file, `List<Goal>`, camelCase, nulls omitted. PrPath is derived from `Goal.Path` — never set directly.

## GoalFile

New class: `PLang/App/modules/builder/GoalFile.cs` — **instance, not static**.

A GoalFile IS the `.goal` file format. It parses `.goal` text into App `Goal` and `Step` objects directly.

### .goal file format

```
GoalName
/ comment about the goal
/ another comment line

- step text here
    - indented sub-step (4 spaces = 1 indent level)
- next step
  continuation line (indented, no dash = appends to previous step)


SecondGoal
/ this is a private sub-goal (not first in file)

- step in second goal
```

### Rules

- **Blank lines** — skip (boundary for comment attribution)
- **`/` lines** — comments. Before first goal → goal comment. Between steps → step comment. `/* ... */` multi-line also supported.
- **`-` lines** — steps. Leading whitespace before `-` = indent level (4 spaces = 1). Indent 0 = top-level.
- **Indented non-dash lines** — continuation of previous step text (appended with `\n`)
- **Everything else** — goal header. First = Public, rest = Private.
- **Tabs → 4 spaces** before parsing

### Path computation

`GoalFile.Parse()` only sets `Path` on each Goal. `PrPath` is derived automatically by the Goal type — never set directly.

For `{rootPath}/folder/MyGoal.goal`:
- All goals get `Path = "/folder/MyGoal.goal"`
- `PrPath` auto-derives to `/folder/.build/mygoal.pr`

### What it produces

Each **Goal**: Name, Description, Comment, Visibility (first = Public, rest = Private), Path, Steps, SubGoals (names of non-first goals, on the public goal), IsSetup, Hash.

Each **Step**: Index, Text (continuations joined by `\n`), LineNumber, Indent, Comment, empty Actions (LLM fills these), Goal back-reference.

## Step.Merge

Add to `Step` (partial class or directly on `this.cs`):

```csharp
/// <summary>
/// Merges LLM-derived fields from another step onto this step.
/// Structural fields (Text, Index, Indent, LineNumber) are untouched.
/// </summary>
public void Merge(Step from) { ... }
```

Step owns knowledge of which fields are LLM-derived (Actions, Cache, OnError) vs structural (Text, Index, Indent). The merge copies LLM-derived mutable fields and replaces Errors/Warnings list contents when the source has any.

## Goal.MergeFrom

Add to `Goal` (partial class or directly on `this.cs`):

```csharp
/// <summary>
/// Merges LLM-derived fields from an existing built goal onto this freshly-parsed goal.
/// Matches steps by Text, delegates to Step.Merge for each match.
/// </summary>
public void MergeFrom(Goal existing) { ... }
```

Goal owns the knowledge of how to match its steps to an existing goal's steps. Match by `Step.Text` — when a step's text hasn't changed, its LLM-derived Actions are still valid. For each match, delegate to `Step.Merge(existingStep)`. Unmatched steps keep their empty Actions (LLM will fill them).

## Actions

All actions implement `IContext`. They navigate to engine via `Context.Engine`.

### builder.getActions

Returns all registered actions with parameter schemas for the LLM prompt.

```csharp
[Action("getActions")]
public partial class getActions : IContext
{
    public Task<Data> Run()
    {
        // Navigate engine.Modules (don't construct new)
        // For each module + action, build parameter metadata from the action type
        // Return as StepActions with Action records containing Module, ActionName,
        //   ParameterSchema, Parameters (as Data list with type info), Cacheable
    }
}
```

Parameter metadata comes from reflecting the action type's public properties — PLang type names via `TypeMapping.GetTypeName()`, nullable detection, `[VariableName]`, `[Default]`, `[Action(Cacheable)]`. Exclude `EqualityContract` and `Context`.

### builder.getTypeInfo

Returns PLang type names and complex type JSON schemas for the LLM prompt.

```csharp
[Action("getTypeInfo")]
public partial class getTypeInfo : IContext
{
    public Task<Data> Run()
    {
        // Delegates to TypeMapping.GetBuilderTypeNames() and GetComplexTypeSchemas()
    }
}
```

### builder.getGoals

Parses `.goal` files from a path, excludes system goals, merges existing `.pr` data.

```csharp
[Action("getGoals")]
public partial class getGoals : IContext
{
    public partial string Path { get; init; }

    public async Task<Data> Run()
    {
        // Find .goal files under Path (via engine.RunAction with file module)
        // Parse each via GoalFile
        // Filter out system goals (path starts with /system/)
        // Merge existing .pr data: load List<Goal> from PrPath, match by Name,
        //   delegate to goal.MergeFrom(existingGoal)
        // Return List<Goal>
    }
}
```

**Merge from .pr**: If a `.pr` file exists at PrPath, deserialize as `List<Goal>`. Match goals by Name. For each match, call `goal.MergeFrom(existingGoal)` — Goal owns the step-matching and merge logic. Preserves LLM work across rebuilds.

### builder.validateActions

Validates LLM-returned actions exist, resolves GoalCall paths, fills defaults.

```csharp
[Action("validateActions")]
public partial class validateActions : IContext
{
    [IsNotNull]
    public partial StepActions Actions { get; init; }

    public async Task<Data> Run()
    {
        // Check each action exists in engine.Modules — error if not found
        // Resolve GoalCall paths: scan .build/ for .pr files, fall back to .goal files
        //   Skip dynamic names (containing %)
        // Fill defaults from [Default] attributes or IConfigure<T> config
        // Return Data.Ok(true)
    }
}
```

### builder.mergeStep

Thin delegation to `Step.Merge()`.

```csharp
[Action("mergeStep")]
public partial class mergeStep : IContext
{
    [IsNotNull]
    public partial Step Step { get; init; }

    [IsNotNull]
    public partial Step StepFromLlm { get; init; }

    public Task<Data> Run()
    {
        Step.Merge(StepFromLlm);
        return Task.FromResult(Data.Ok(Step));
    }
}
```

### builder.getApp

Loads or creates `.build/app.pr`. Returns `AppData`.

```csharp
[Action("getApp")]
public partial class getApp : IContext
{
    [Default(".")]
    public partial string Path { get; init; }

    public async Task<Data> Run()
    {
        // Read .build/app.pr via engine.RunAction(file.read)
        // If exists → deserialize as AppData
        // If not → create new AppData (new GUID, Version = "0.2"), save it
    }
}
```

### builder.saveApp

Saves `AppData` back to disk.

```csharp
[Action("saveApp")]
public partial class saveApp : IContext
{
    [IsNotNull]
    public partial AppData App { get; init; }

    [Default(".build/app.pr")]
    public partial string Path { get; init; }

    public async Task<Data> Run()
    {
        // Update App.Updated timestamp
        // Write via engine.RunAction(file.write)
    }
}
```

### builder.saveGoals

Serializes goals to a v0.2 `.pr` file.

```csharp
[Action("saveGoals")]
public partial class saveGoals : IContext
{
    [IsNotNull]
    public partial List<Goal> Goals { get; init; }

    public async Task<Data> Run()
    {
        // PrPath from first goal (all share the same derived PrPath)
        // Serialize List<Goal> — camelCase, nulls omitted
        // Write via engine.RunAction(file.write)
    }
}
```

## How the Builder Goals Map

```
Build.goal                          → builder module action
─────────────────────────────────────────────────────────────────
[plang] call GetApp                 → builder.getApp
[plang] GetGoalsV2                  → builder.getGoals
[plang] call SaveApp                → builder.saveApp
[plang] get all actions             → builder.getActions
[plang] get type info               → builder.getTypeInfo
[plang] ValidateActions             → builder.validateActions
[plang] MergeStep                   → builder.mergeStep
[plang] call SaveGoal               → builder.saveGoals
```

After this module lands, `[plang]` tags become regular `builder.*` calls.

## File Structure

```
PLang/App/modules/builder/
├── GoalFile.cs            — .goal file format (instance, not static)
├── getActions.cs          — action registry metadata for LLM
├── getTypeInfo.cs         — type names + schemas for LLM
├── getGoals.cs            — find + parse .goal files, delegate merge to Goal
├── validateActions.cs     — validate + resolve + fill defaults
├── mergeStep.cs           — delegates to Step.Merge()
├── getApp.cs              — load/create app.pr
├── saveApp.cs             — save app.pr
├── saveGoals.cs           — save goals as .pr file
```

## Files to Modify

| File | Change |
|------|--------|
| `PLang/App/Goals/Goal/this.cs` | Add `MergeFrom(Goal existing)` method |
| `PLang/App/Goals/Goal/Steps/Step/this.cs` | Add `Merge(Step from)` method |
| `system/builder/Build.goal` | Replace `[plang]` calls with `builder.*` |
| `system/builder/BuildGoal.goal` | Replace `[plang]` calls with `builder.*` |
| `system/builder/BuildStep.goal` | Replace `[plang]` calls with `builder.*` |
| `system/builder/ApplyStep.goal` | Replace `[plang]` calls with `builder.*` |

## Test Expectations

### GoalFile (~8)
- Single goal with steps
- Multiple goals (first public, rest private)
- Indentation (4 spaces = 1 level)
- Continuation lines appended
- Comments on goals and steps
- Multi-line `/* */` comments
- All goals share Path, PrPath derives correctly
- Empty file → empty list

### getActions (~5)
- Returns all modules and actions
- Parameter types with nullable markers
- `@var` parameters marked
- `[Default]` values included
- `Cacheable` flag from `[Action]`

### getTypeInfo (~2)
- Type names from TypeMapping
- Complex type schemas

### getGoals (~4)
- Parses .goal files from folder
- Excludes system goals
- Merges .pr data by Text match
- Empty folder → empty list

### validateActions (~5)
- Valid actions pass
- Unknown action → error with names
- GoalCall PrPath resolved
- Dynamic names (%) skipped
- Defaults filled from `[Default]`

### mergeStep (~3)
- Actions copied
- Cache/OnError copied when present
- Errors/warnings replaced

### getApp (~2)
- Loads existing app.pr
- Creates new when missing (GUID, Version = "0.2")

### saveGoals (~2)
- Serializes to PrPath, camelCase, nulls omitted
- Multiple goals in single .pr file

### Goal.MergeFrom (~3)
- Steps matched by Text, LLM fields merged via Step.Merge
- Unmatched steps keep empty Actions
- No existing goal → no-op

### Step.Merge (~3)
- LLM fields copied, structural fields untouched
- Empty source leaves target unchanged
- Errors/warnings replaced only when source has entries

### Test count
- **C# tests:** ~37
- **PLang tests:** ~6
- **Total: ~43**

## Definition of Done

- All 8 builder actions work as standard App module actions
- **Zero Runtime1 dependencies**
- Actions navigate `engine.Modules` — never construct fresh registries
- `GoalFile` is an instance, not static — it IS the file format, not a "parser"
- `Goal.MergeFrom()` owns step-matching and merge delegation
- `Step.Merge()` owns field-level merge — `mergeStep` action just delegates
- `engine.Building.IsEnabled` guard on entry
- `.pr` files read/written as `List<Goal>` — one per `.goal` file
- File I/O through `engine.RunAction`
- Builder goal files updated from `[plang]` to `builder.*`
- C# and PLang tests pass
