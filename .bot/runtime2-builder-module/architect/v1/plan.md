# Piece 8: Builder Module

> **Note for coder**: This module lives in `PLang.Runtime2.modules.builder` and follows the standard action pattern (partial record, `[Action]`, `IContext`, source-generated lazy params). Zero Runtime1 dependencies — no `IGoalParser`, no `PrParser`, no `GoalMapper`. Everything works directly with Runtime2 types. Read existing modules (`file`, `http`, `llm`) as reference for patterns.

## Decision Log

- **No Runtime1 dependencies.** No `IGoalParser`, no `PrParser`, no `GoalMapper`, no `Building.Model`. The builder module parses `.goal` files directly into Runtime2 Goal/Step types and reads `.pr` files as Runtime2 JSON.
- **Native `.goal` parser.** A new `GoalFileParser` class in the builder module parses `.goal` text files directly into Runtime2 `Goal` and `Step` objects. Simple line-by-line parser — no Sprache dependency, no v1 model intermediary.
- **`.pr` files are multi-goal Runtime2 JSON.** One `.goal` file → one `.pr` file containing `List<Goal>`. Reading existing `.pr` files is `JsonSerializer.Deserialize<List<Goal>>(json)`. No conversion, no mapping. All goals from a single `.goal` file share the same `Path` and therefore the same derived `PrPath` — they belong in one file.
- **No provider pattern.** The builder module has no swappable behavior. It's a thin layer over `EngineModules`, `TypeMapping`, file I/O, and the goal parser.
- **File I/O goes through `engine.RunAction(file.read/file.write)`.** Follows the same convention as the LLM module using `http.request`.
- **`EngineModules` is instantiated fresh.** Each `getActions` call creates a `new EngineModules()` which discovers all `[Action]` types in the PLang assembly.
- **MergeStep copies LLM-derived fields only.** Takes the target step (from parser, with structural fields like Text/Index/Indent already set via init) and the LLM result step. Copies only the LLM-derived mutable fields: Actions (`set`), Cache (`set`), OnError (`set`). For Errors/Warnings (init-only lists), mutates the existing list contents via `Clear()`/`AddRange()`. Returns the mutated step.
- **ValidateActions does three things.** (1) Checks all actions exist in `EngineModules`, (2) resolves `GoalCall.PrPath` for goal-type parameters, (3) fills default values from `[Default]` attributes or `IConfigure<T>` config instances.
- **GoalCall path resolution uses the file system directly.** Scans `.build/` for existing `.pr` files, or scans the source tree for `.goal` files, to find matching goals. No PrParser.
- **SaveGoal uses v0.2 format.** Single `.pr` file per `.goal` file, containing `List<Goal>` (one or more goals). Serialized with camelCase, nulls omitted. PrPath is derived from `Goal.Path` — never set directly.
- **App.pr is simple JSON.** `GetApp` loads or creates `.build/app.pr`. `SaveApp` writes it back. Both use `AppData` from `PLang.Runtime2.Engine.Utility`.
- **Engine.Building guard.** `engine.Building` is a `Build.@this` object (not a bool). Builder module actions guard on `engine.Building != null` — null means not in build mode. If null, return `Data.FromError`. Guard on `Run()`.

## GoalFileParser

New class: `PLang/Runtime2/modules/builder/GoalFileParser.cs`

Parses `.goal` text files directly into Runtime2 `Goal` and `Step` objects. No intermediary types.

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

### Parsing rules

1. **Blank lines** — skip (but mark boundary for comment attribution)
2. **`/` lines** — comments. Before first goal = goal comment. Between steps = step comment. `/* ... */` multi-line comments also supported.
3. **`-` lines** — steps. Leading whitespace before `-` = indent level (must be multiple of 4). Indent 0 = top-level step.
4. **Indented non-dash lines** — continuation of previous step's text (appended with `\n`)
5. **Everything else** — goal header. First goal in file = Public visibility. Subsequent goals = Private.
6. **Tabs → 4 spaces** before parsing

### Path computation

GoalFileParser only sets `Path` on each Goal. `PrPath` is a computed property on `Goal` — derived automatically from `Path`. Never set PrPath directly (the init setter is an intentional no-op).

For a `.goal` file at `{rootPath}/folder/MyGoal.goal`:
- All goals in the file get `Path = "/folder/MyGoal.goal"`
- `PrPath` auto-derives to `/folder/.build/mygoal.pr` (sibling `.build/` folder, lowercase)
- All goals from one file share the same PrPath → stored together in one `.pr` file as `List<Goal>`

### Output

```csharp
public static class GoalFileParser
{
    /// <summary>
    /// Parses a .goal file into Runtime2 Goal objects.
    /// Returns one Goal per goal block in the file.
    /// First goal is Public, rest are Private.
    /// </summary>
    public static List<Goal> Parse(string content, string relativeGoalPath)
    {
        // Line-by-line parser producing Runtime2 Goal + Step objects directly
    }
}
```

### What it produces

Each `Goal` gets:
- `Name` — from goal header line
- `Description` / `Comment` — from `/` comment lines
- `Visibility` — first = Public, rest = Private
- `Path` — relative .goal file path (same for all goals in one file)
- `PrPath` — **not set** — auto-derived from `Path` by the Goal type
- `Steps` — `Steps.@this` collection (extends `List<Step>`)
- `SubGoals` — list of sub-goal names (on the first/public goal)
- `IsSetup` — true if goal name starts with "Setup" or step text matches setup patterns
- `Hash` — hash of the goal text content

Each `Step` gets:
- `Index` — 0-based position in goal
- `Text` — step text (with continuations joined by `\n`)
- `LineNumber` — source line number
- `Indent` — indent level (spaces / 4)
- `Comment` — from preceding `/` lines
- `Actions` — empty `StepActions` (LLM fills these during build)
- `Goal` — back-reference to parent goal

## Actions

### builder.getActions

Returns all registered actions with their parameter schemas. Used by the builder to tell the LLM what actions are available.

```csharp
[Action("getActions")]
public partial class getActions : IContext
{
    public async Task<Data> Run()
    {
        var modules = new EngineModules();
        var actions = new StepActions(this.Context);

        foreach (var ns in modules.Names)
        {
            foreach (var className in modules.GetActions(ns))
            {
                var parameterType = modules.GetActionType(ns, className);
                if (parameterType == null) continue;

                var parameters = BuildParameterList(parameterType);
                var cacheable = GetCacheable(parameterType);

                actions.Add(new Action
                {
                    Module = ns,
                    ActionName = className,
                    ParameterSchema = parameterType,
                    Parameters = parameters,
                    Cacheable = cacheable
                });
            }
        }

        return Data.Ok(actions);
    }
}
```

**Parameter inspection**:
- Iterates public instance properties (excluding `EqualityContract`, `Context`)
- Uses `TypeMapping.GetTypeName()` for PLang type names
- Detects nullable via `NullabilityInfoContext`
- Appends valid values inline: `actor(user|service|system)`
- Marks `@var` parameters via `[VariableName]` attribute
- Reads `[Default]` attribute values
- Reads `[Action(Cacheable=false)]` flag

### builder.getTypeInfo

Returns PLang type names and complex type JSON schemas. Used in builder prompts so the LLM knows available types.

```csharp
[Action("getTypeInfo")]
public partial class getTypeInfo : IContext
{
    public Task<Data> Run()
    {
        var names = TypeMapping.GetBuilderTypeNames();
        var schemas = TypeMapping.GetComplexTypeSchemas();
        var schemaLines = schemas.Select(kvp => $"  {kvp.Key}: {kvp.Value}");

        return Task.FromResult(Data.Ok(new
        {
            TypeNames = string.Join(", ", names),
            TypeSchemas = string.Join("\n", schemaLines)
        }));
    }
}
```

### builder.getGoals

Parses `.goal` files from a path and returns Runtime2 Goal objects. Excludes system goals. Merges existing `.pr` data (previously built actions) back onto matching steps.

```csharp
[Action("getGoals")]
public partial class getGoals : IContext
{
    /// <summary>File or folder path to scan for .goal files.</summary>
    public partial string Path { get; init; }

    public async Task<Data> Run()
    {
        // 1. Find all .goal files under Path (via engine.RunAction(file.find) or glob)
        // 2. For each file, read content
        // 3. Parse via GoalFileParser.Parse(content, relativePath)
        // 4. Filter out system goals (path starts with /system/)
        // 5. For each goal, merge existing .pr data (MergePrData)
        // 6. Return Data.Ok(List<Goal>)
    }
}
```

**MergePrData** (private helper): If a `.pr` file already exists (at the shared `PrPath`), reads it as `JsonSerializer.Deserialize<List<Goal>>(json)`. Matches parsed goals to existing goals by `Name`, then matches steps within each goal by `Text`. Copies previously-built `Actions` onto matching parsed steps. This preserves LLM work across rebuilds — unchanged steps keep their existing actions.

### builder.validateActions

Validates LLM-returned actions exist in the module registry, resolves GoalCall paths, and fills default parameter values.

```csharp
[Action("validateActions")]
public partial class validateActions : IContext
{
    /// <summary>Actions to validate (from LLM response).</summary>
    [IsNotNull]
    public partial StepActions Actions { get; init; }

    public async Task<Data> Run()
    {
        // 1. Check each action exists in EngineModules
        //    - If any not found, return Data.FromError with list
        // 2. ResolveGoalCallPaths — for goal.call typed params,
        //    scan .build/ for .pr files or source tree for .goal files
        //    and set GoalCall.PrPath
        // 3. FillDefaults — for each action, add missing [Default]
        //    values or IConfigure<T> defaults to Action.Defaults
        // 4. Return Data.Ok(true)
    }
}
```

**ResolveGoalCallPaths**:
- Scans parameters for `Type == "goal.call"`
- Deserializes `GoalCall` from value (handles JsonElement, string, or already GoalCall)
- If name contains `%` → dynamic, skip path resolution
- Scans `.build/` directory tree for `.pr` files matching the goal name
- Falls back to scanning source tree for `.goal` files
- Sets `GoalCall.PrPath` for build-time optimization

**FillDefaults**:
- For each action, find parameters NOT provided by the LLM
- If action implements `IConfigure<TConfig>` → instantiate TConfig, read C# default values
- Otherwise → read `[Default]` attribute values
- Store as `Action.Defaults` list

### builder.mergeStep

Merges LLM step result into an existing step. Copies actions, cache, onError, errors, warnings.

```csharp
[Action("mergeStep")]
public partial class mergeStep : IContext
{
    /// <summary>Target step to merge into.</summary>
    [IsNotNull]
    public partial Step Step { get; init; }

    /// <summary>LLM result step to merge from.</summary>
    [IsNotNull]
    public partial Step StepFromLlm { get; init; }

    public Task<Data> Run()
    {
        Step.Actions.Clear();
        Step.Actions.AddRange(StepFromLlm.Actions);

        if (StepFromLlm.Cache != null)
            Step.Cache = StepFromLlm.Cache;

        if (StepFromLlm.OnError != null)
            Step.OnError = StepFromLlm.OnError;

        if (StepFromLlm.Errors.Count > 0)
        {
            Step.Errors.Clear();
            Step.Errors.AddRange(StepFromLlm.Errors);
        }
        if (StepFromLlm.Warnings.Count > 0)
        {
            Step.Warnings.Clear();
            Step.Warnings.AddRange(StepFromLlm.Warnings);
        }

        return Task.FromResult(Data.Ok(Step));
    }
}
```

### builder.getApp

Loads or creates the `app.pr` file. Contains app GUID, name, version.

```csharp
[Action("getApp")]
public partial class getApp : IContext
{
    /// <summary>App folder path. Default: current directory.</summary>
    [Default(".")]
    public partial string Path { get; init; }

    public async Task<Data> Run()
    {
        // 1. Compute .build/app.pr path
        // 2. Read via engine.RunAction(file.read)
        // 3. If exists, deserialize as AppData
        // 4. If not, create new AppData with new GUID, save it
        // 5. Return Data.Ok(appData)
    }
}
```

### builder.saveApp

Saves the `app.pr` file back to disk. Updates the `Updated` timestamp.

```csharp
[Action("saveApp")]
public partial class saveApp : IContext
{
    /// <summary>App data to save.</summary>
    [IsNotNull]
    public partial AppData App { get; init; }

    /// <summary>File path override. Default: .build/app.pr</summary>
    [Default(".build/app.pr")]
    public partial string Path { get; init; }

    public async Task<Data> Run()
    {
        // 1. Set App.Updated = DateTime.UtcNow
        // 2. Serialize with camelCase, indented
        // 3. Write via engine.RunAction(file.write)
        // 4. Return Data.Ok(new { Path })
    }
}
```

### builder.saveGoals

Serializes a list of Runtime2 Goals to a v0.2 `.pr` file (all goals from one `.goal` file in one `.pr` file).

```csharp
[Action("saveGoals")]
public partial class saveGoals : IContext
{
    /// <summary>Goals to save (all from the same .goal file).</summary>
    [IsNotNull]
    public partial List<Goal> Goals { get; init; }

    public async Task<Data> Run()
    {
        // 1. Get PrPath from first goal (all share the same derived PrPath)
        // 2. Ensure directory exists
        // 3. Serialize List<Goal> with camelCase, indented, nulls omitted
        // 4. Write via engine.RunAction(file.write)
        // 5. Return Data.Ok(new { Path, Format = "v0.2" })
    }
}
```

## Dependencies

All Runtime2, no Runtime1:

| Dependency | Used by | Notes |
|------------|---------|-------|
| `EngineModules` | `getActions`, `validateActions` | Action registry — discovers all [Action] types |
| `TypeMapping` | `getActions`, `getTypeInfo` | PLang type names, valid values, complex type schemas |
| `AppData` | `getApp`, `saveApp` | Simple POCO for app.pr |
| `GoalFileParser` | `getGoals` | New — parses .goal text → Runtime2 Goal/Step |
| `file.write` | `saveApp`, `saveGoals` | File I/O via engine.RunAction |
| `file.read` | `getApp`, `getGoals` | File I/O via engine.RunAction |

## How the Builder Goals Map

```
Build.goal                          → builder module actions used
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

After this module lands, the `[plang]` tag in builder goal files becomes `builder.actionName` — regular module calls, no special handling needed.

## File Structure

```
PLang/Runtime2/modules/builder/
├── GoalFileParser.cs      — .goal text file parser → Runtime2 Goal/Step
├── getActions.cs          — action registry metadata for LLM
├── getTypeInfo.cs         — type names + schemas for LLM
├── getGoals.cs            — find + parse .goal files, merge .pr data
├── validateActions.cs     — validate + resolve + fill defaults
├── mergeStep.cs           — merge LLM result into step
├── getApp.cs              — load/create app.pr
├── saveApp.cs             — save app.pr
├── saveGoals.cs           — save goals list as .pr file
```

No providers directory. No types file (uses existing types from Engine/).

## Test Expectations

### C# unit tests (~30)

**GoalFileParser (8):**
- Parses single goal with steps
- Parses multiple goals (first public, rest private)
- Step indentation tracked correctly (4 spaces = indent 1)
- Continuation lines appended to previous step text
- Comments attributed to goals and steps correctly
- Multi-line `/* */` comments handled
- All goals share same Path, PrPath auto-derives correctly
- Empty/whitespace-only file returns empty list

**getActions (5):**
- Returns all registered modules and their actions
- Parameter list includes type names with nullable markers
- `@var` parameters marked correctly
- `[Default]` values included in parameter description
- `Cacheable` flag read from `[Action]` attribute

**getTypeInfo (2):**
- TypeNames includes all builder type names from TypeMapping
- TypeSchemas includes complex type JSON schemas

**getGoals (4):**
- Parses .goal files from folder path
- Excludes system goals
- Merges existing .pr data onto matching steps (by Text)
- Returns empty list for folder with no .goal files

**validateActions (5):**
- Valid actions pass validation
- Unknown module.action returns error with names
- GoalCall parameters get PrPath resolved via .build/ scan
- Dynamic goal names (containing %) skip path resolution
- Missing parameters filled from [Default] attributes

**mergeStep (3):**
- Actions copied from LLM step to target step
- Cache and OnError copied when present on LLM step
- Errors/warnings copied and replace existing

**getApp (2):**
- Loads existing app.pr and deserializes
- Creates new app.pr when none exists (generates GUID, Version = "0.2")

**saveGoals (2):**
- Serializes List<Goal> to PrPath with camelCase, null properties omitted
- Multiple goals from one file stored in single .pr file

### PLang tests (~6)

- getApp creates and retrieves app.pr
- getGoals parses a test .goal file
- validateActions passes for known actions, fails for unknown
- mergeStep copies actions from LLM result
- saveGoals writes .pr file, re-read matches
- Full build flow: getApp → getGoals → (simulate LLM) → validateActions → mergeStep → saveGoals

### Test count
- **C# tests:** ~31
- **PLang tests:** ~6
- **Total: ~37**

## Files to Create

| File | Purpose |
|------|---------|
| `PLang/Runtime2/modules/builder/GoalFileParser.cs` | .goal text parser → Runtime2 types |
| `PLang/Runtime2/modules/builder/getActions.cs` | Action registry metadata |
| `PLang/Runtime2/modules/builder/getTypeInfo.cs` | Type names + schemas |
| `PLang/Runtime2/modules/builder/getGoals.cs` | Find + parse .goal files |
| `PLang/Runtime2/modules/builder/validateActions.cs` | Validate + resolve + defaults |
| `PLang/Runtime2/modules/builder/mergeStep.cs` | Merge LLM result into step |
| `PLang/Runtime2/modules/builder/getApp.cs` | Load/create app.pr |
| `PLang/Runtime2/modules/builder/saveApp.cs` | Save app.pr |
| `PLang/Runtime2/modules/builder/saveGoals.cs` | Save goals list as .pr |

## Files to Modify

| File | Change |
|------|--------|
| `system/builder/Build.goal` | Replace `[plang]` calls with `builder.*` actions |
| `system/builder/BuildGoal.goal` | Replace `[plang]` calls with `builder.*` actions |
| `system/builder/BuildStep.goal` | Replace `[plang]` calls with `builder.*` actions |
| `system/builder/ApplyStep.goal` | Replace `[plang]` calls with `builder.*` actions |

## Definition of Done

- All 8 builder actions work as Runtime2 module actions
- **Zero Runtime1 dependencies** — no IGoalParser, no PrParser, no GoalMapper, no Building.Model
- `GoalFileParser` parses .goal text files directly into Runtime2 Goal/Step objects
- `.pr` files read as Runtime2 Goal JSON (no v1→v2 conversion)
- `getActions` returns full action registry with parameter schemas, types, defaults, cacheable flags
- `getTypeInfo` returns type names and complex type schemas for builder prompts
- `getGoals` parses .goal files, excludes system goals, merges existing .pr data
- `validateActions` checks action existence, resolves GoalCall paths via file system scan, fills defaults
- `mergeStep` copies actions/cache/onError/errors/warnings from LLM step to target
- `getApp`/`saveApp` manage app.pr lifecycle
- `saveGoals` writes v0.2 `.pr` files as `List<Goal>` (camelCase, nulls omitted) — one `.pr` per `.goal` file
- File I/O goes through `engine.RunAction(file.read/write)`
- Builder goal files updated to use `builder.*` instead of `[plang]`
- `[plang]` tag no longer needed for builder operations
- C# and PLang tests pass
