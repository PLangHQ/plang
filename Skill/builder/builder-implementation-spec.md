# PLang Builder — C# Implementation Spec

This document specifies what C# code needs to be built to support the PLang builder. The builder itself is written in PLang — C# only provides the infrastructure methods and data structures.

---

## 1. Data Structures

### 1.1 App

```csharp
public partial class App
{
    public string Id { get; set; }
    public DateTime Created { get; set; }
    public DateTime Updated { get; set; }
}
```

### 1.2 Goal

```csharp
public partial class Goal
{
    // Deterministic — set by parser
    public string Path { get; set; }
    public string PrPath { get; set; }
    public List<Step> Steps { get; set; } = new();

    // Build metadata
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
```

### 1.3 Step

```csharp
public partial class Step
{
    // Deterministic — set by parser, never modified by LLM
    public string Text { get; set; }
    public DateTime Created { get; set; }
    public DateTime Update { get; set; }
    public string Hash { get; set; }
    public string? PreviousHash { get; set; }
    public int Index { get; set; }
    public int LineNumber { get; set; }

    // LLM Pass 1 — module selection
    public string? Module { get; set; }
    public string? Intent { get; set; }

    // LLM Pass 2 — step building
    public string? Method { get; set; }
    public StepData? Data { get; set; }
    public string? Return { get; set; }
    public ErrorHandler? OnError { get; set; }
    public CacheSettings? Cache { get; set; }
    public int? CancelAfterSeconds { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
```

### 1.4 StepData

```csharp
public class StepData
{
    public string Type { get; set; }    // e.g. "db.select", "http.request"
    public object Data { get; set; }    // the typed request object
}
```

### 1.5 ErrorHandler

```csharp
public class ErrorHandler
{
    public GoalToCallInfo? Goal { get; set; }
    public int? RetryCount { get; set; }
    public int? RetryOverMs { get; set; }
    public ErrorOrder? Order { get; set; }
    public bool IgnoreError { get; set; }
    public string? Message { get; set; }
    public int? StatusCode { get; set; }
    public string? Key { get; set; }
}

public enum ErrorOrder
{
    GoalFirst,
    RetryFirst
}
```

### 1.6 GoalToCallInfo

Reusable wherever PLang references a goal — error handlers, channel bindings, event handlers, `call goal` steps.

```csharp
public class GoalToCallInfo
{
    public string Name { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
}
```

### 1.7 CacheSettings

```csharp
public class CacheSettings
{
    public int DurationMinutes { get; set; }
    public bool Sliding { get; set; }
    public string? Key { get; set; }
}
```

### 1.8 Build Error/Warning Keys

Fixed string constants the LLM selects from. Defined as constants so they can be included in prompt documentation.

```csharp
public static class BuildErrors
{
    public const string MissingVariable = "MissingVariable";
    public const string ModuleNotFound = "ModuleNotFound";
    public const string MethodNotFound = "MethodNotFound";
    public const string InvalidParameter = "InvalidParameter";
}

public static class BuildWarnings
{
    public const string AmbiguousModule = "AmbiguousModule";
    public const string AmbiguousMethod = "AmbiguousMethod";
    public const string MissingErrorHandler = "MissingErrorHandler";
    public const string NoWriteTo = "NoWriteTo";
    public const string UnusedVariable = "UnusedVariable";
}
```

---

## 2. C# Module Methods

These seven methods are exposed as PLang module calls. They belong to a builder module (e.g., `BuilderModule : BaseModule`).

### 2.1 GetApp

**PLang call:**
```plang
- get plang app, parser:"app", write to %app%
```

**C# signature:**
```csharp
public Task<GoalResult> GetApp(string parser)
```

**Behavior:**
1. Check if `/.build/app.pr` exists
2. If it exists, deserialize and return the existing `App` object (preserving `Id` and `Created`)
3. If it doesn't exist, create a new `App`:
   - `Id` — generate a new unique identifier (GUID or similar)
   - `Created` — current timestamp
   - `Updated` — current timestamp
4. Return `GoalResult.Success(App)`

### 2.2 GetAllGoals

**PLang call:**
```plang
- get all goals, parser:"goal", write to %goals%
```

**C# signature:**
```csharp
public Task<GoalResult> GetAllGoals(string parser)
```

**Behavior:**
1. Scan the project directory for all `.goal` files
2. Parse each file using the specified parser (currently only `"goal"` parser exists)
3. For each goal, extract steps from the natural language text
4. Set deterministic properties on each step:
   - `Text` — the step text (everything after `- ` on that line, including indented continuation lines for error handling, caching, cancellation)
   - `Created` — file creation time (or first seen time)
   - `Update` — current timestamp
   - `Hash` — hash of the step's `Text` content
   - `Index` — zero-based position in the goal
   - `LineNumber` — line number in the `.goal` file
5. Load `PreviousHash` from the existing `.pr` file if one exists (for change detection)
6. Set `Goal.Path` from the goal name and file location
7. Set `Goal.PrPath` to the corresponding `.pr` output path
8. Return `GoalResult.Success(List<Goal>)`

**Goal file parsing rules:**
- First non-empty line is the goal name
- Lines starting with `- ` are steps
- Lines starting with `/` are comments (ignored)
- Indented lines below a step are continuation lines (part of that step's `Text`)

### 2.3 GetModules

**PLang call:**
```plang
- get plang modules, format:"md", write to %modules%
```

**C# signature:**
```csharp
public Task<GoalResult> GetModules(string format)
```

**Behavior:**
1. Query the `ModuleRegistry` for all registered modules
2. Format as markdown (when `format` is `"md"`):
   - Module name
   - Brief description of what the module handles
   - Example step patterns it responds to
3. Return `GoalResult.Success(string)` — the markdown content

**Example output:**
```markdown
## db
Handles database operations — queries, inserts, updates, deletes.
Example steps: "select * from ...", "insert into ...", "update ... set ...", "delete from ..."

## http
Handles HTTP requests — GET, POST, PUT, DELETE to external APIs.
Example steps: "get https://...", "post https://... data: ..."

## io
Handles input/output — writing to channels, console output.
Example steps: "write out ...", "write out to system ..."

## llm
Handles LLM/AI calls — sending prompts, getting structured responses.
Example steps: "[llm] system: ... user: ... scheme: ..."
```

### 2.4 GetMethods

**PLang call:**
```plang
- get plang methods for %module%, format:"md", write to %methods%
```

**C# signature:**
```csharp
public Task<GoalResult> GetMethods(string module, string format)
```

**Behavior:**
1. Look up the module in `ModuleRegistry`
2. Retrieve its method list with signatures, data types, and type definitions
3. Format as markdown (when `format` is `"md"`):
   - Method name
   - Data type it accepts (with full type definition)
   - Return type
   - Shared type definitions (ErrorHandler, CacheSettings, GoalToCallInfo, etc.)
4. Return `GoalResult.Success(string)` — the markdown content

**Example output for `db` module:**
```markdown
## db Module

### Methods

#### select
- data type: `db.select`
- return type: `object`

#### insert
- data type: `db.insert`
- return type: `object`

#### update
- data type: `db.update`
- return type: `object`

#### delete
- data type: `db.delete`
- return type: `object`

### Data Types

#### db.select
{ sql: string(required), parameters?: [{key: value}] = null }

#### db.insert
{ sql: string(required), parameters?: [{key: value}] = null }

#### db.update
{ sql: string(required), parameters?: [{key: value}] = null }

#### db.delete
{ sql: string(required), parameters?: [{key: value}] = null }

### Shared Types

#### ErrorHandler
{ goal?: GoalToCallInfo, retryCount?: int, retryOverMs?: int, order?: "GoalFirst" | "RetryFirst", ignoreError?: bool, message?: string, statusCode?: int, key?: string }

#### GoalToCallInfo
{ name: string, parameters?: object }

#### CacheSettings
{ durationMinutes: int, sliding: bool, key?: string }
```

### 2.5 SaveGoal

**PLang call:**
```plang
- save plang %goal% to %goal.PrPath%
```

**C# signature:**
```csharp
public Task<GoalResult> SaveGoal(Goal goal, string path)
```

**Behavior:**
1. Serialize the `Goal` object to JSON
2. Include all step properties (deterministic + LLM-derived)
3. Do **not** include transient build properties (like `PreviousHash` — this is only used during build)
4. Write to the specified path
5. Return `GoalResult.Success()`

**Serialization rules:**
- Null/empty optional fields should be omitted from the JSON (not written as `null`)
- `Data.Data` is serialized as-is (the object the LLM produced)
- `%variable%` strings are stored literally — they get resolved at runtime by the MemoryStack

### 2.6 SaveApp

**PLang call:**
```plang
- save plang %app% to .build/app.pr
```

**C# signature:**
```csharp
public Task<GoalResult> SaveApp(App app, string path)
```

**Behavior:**
1. Set `app.Updated` to current timestamp
2. Serialize the `App` object to JSON
3. Write to the specified path
4. Return `GoalResult.Success()`

### 2.7 Validate

**PLang call:**
```plang
- validate %step.Module%, %stepResult.method%, %stepResult.data%
    on error call RetryBuildStep
```

**C# signature:**
```csharp
public Task<GoalResult> Validate(string module, string method, StepData data)
```

**Behavior:**
1. Check the module exists in `ModuleRegistry` — if not, throw error: `"Module '{module}' not found in registry"`
2. Check the method exists on that module — if not, throw error: `"Method '{method}' not found on module '{module}'"`
3. Check the `data.Type` matches the expected data type for that method — if not, throw error: `"Data type '{data.Type}' does not match expected type for {module}.{method}"`
4. Check the `data.Data` object has the required fields for that data type — if missing required fields, throw error listing which fields are missing
5. If all checks pass, return `GoalResult.Success()`

Errors thrown by this method are accessible via `%!error%` in PLang. The `on error` handler on the validate step catches the error and routes to `RetryBuildStep`.

---

## 3. Goal Parser

The goal parser (`parser:"goal"`) is a deterministic C# component that reads `.goal` files. No LLM involvement.

### Parsing Rules

```
GoalName                          ← first non-empty line = goal name
/ this is a comment               ← lines starting with / are comments, ignored
- step one text                   ← lines starting with - are steps
- step two text                   ← new step
    on error call HandleError     ← indented = continuation of previous step
    cache for 10 min              ← still part of step two
    cancel after 30 sec           ← still part of step two
    write to %result%             ← still part of step two
- step three text                 ← new step (back to - prefix)
    - substep                     ← indented with - prefix = sub-step (e.g., inside if/then)
```

**Step text composition:**
The step's `Text` property includes the main line AND all indented continuation lines. So for:

```plang
- select * from users
    on error call HandleError then retry 3 times over 10 seconds
    cache for 10 min from last usage
    cancel after 30 sec
    write to %users%
```

The `Text` would be:
```
select * from users\non error call HandleError then retry 3 times over 10 seconds\ncache for 10 min from last usage\ncancel after 30 sec\nwrite to %users%
```

The LLM receives this full text and parses intent, method, data, error handling, caching, cancellation, and return variable from it.

### Hash Computation

- Hash is computed from the step's `Text` content (including continuation lines)
- Algorithm: SHA256 or similar — fast, deterministic
- Used for change detection: if `Hash == PreviousHash`, skip rebuilding that step

### PreviousHash Loading

- When parsing goals, check if a `.pr` file already exists at `Goal.PrPath`
- If it exists, load it and match steps by `Index`
- Copy the existing step's `Hash` into `PreviousHash` on the new step
- This allows `BuildStep` to compare and skip unchanged steps

---

## 4. File Structure

```
PLang/
├── Runtime2/                    ← Runtime (existing)
│   ├── Engine.cs
│   ├── ...
│   └── GoalData.cs
├── Builder/                     ← Builder C# infrastructure
│   ├── BuilderModule.cs         ← The seven PLang-callable methods
│   ├── GoalParser.cs            ← .goal file parser
│   ├── BuildErrors.cs           ← Error/warning key constants
│   └── Types/
│       ├── ErrorHandler.cs
│       ├── GoalToCallInfo.cs
│       ├── CacheSettings.cs
│       └── StepData.cs
├── goals/builder/               ← Builder PLang goals
│   ├── Build.goal
│   ├── BuildGoal.goal
│   ├── AssignModule.goal
│   ├── BuildStep.goal
│   ├── RetryBuildStep.goal
│   └── HandleBuildStepFailure.goal
├── goals/builder/.build/        ← Hand-built .pr files for bootstrap
│   ├── Build.pr
│   ├── BuildGoal.pr
│   ├── AssignModule.pr
│   ├── BuildStep.pr
│   ├── RetryBuildStep.pr
│   └── HandleBuildStepFailure.pr
└── prompts/                     ← LLM prompt templates
    ├── SelectModules.md
    ├── BuildStep.md
    └── FixStep.md
```

---

## 5. Bootstrap Procedure

The builder is written in PLang but needs `.pr` files to run. Bootstrap order:

1. Implement all C# methods in `BuilderModule.cs` (GetApp, GetAllGoals, GetModules, GetMethods, SaveGoal, SaveApp, Validate)
2. Implement `GoalParser.cs`
3. Implement all data structures
4. Hand-write `.pr` files for the builder goals:
   - `Build.pr`
   - `BuildGoal.pr`
   - `AssignModule.pr`
   - `BuildStep.pr`
   - `RetryBuildStep.pr`
   - `HandleBuildStepFailure.pr`
5. Write the prompt template files (`SelectModules.md`, `BuildStep.md`, `FixStep.md`)
6. Run the builder — it can now build any other PLang project
7. Run the builder on itself — it rebuilds its own `.pr` files from the `.goal` files, replacing the hand-written versions

After step 7, the builder is self-hosting.

---

## 6. Prompt Template Files

### prompts/SelectModules.md

This prompt is loaded with `load vars`, so `%modules%` gets resolved before the LLM sees it.

The prompt should instruct the LLM to:
- Read all steps in the goal
- For each step, select the most appropriate module from the available list
- Optionally write an `intent` describing the user's purpose for that step
- Return an array of `{stepIndex, module, intent?}`
- Consider the overall goal flow when making selections

### prompts/BuildStep.md

This prompt is loaded with `load vars`, so `%methods%` gets resolved.

The prompt should instruct the LLM to:
- Read the step text and intent
- Select the correct method from the module's available methods
- Build the typed `data` object matching the method's expected data type
- Identify the return variable from "write to %variable%" if present
- Parse execution policies (on error, cache, cancel) into their typed objects
- Flag any build errors or warnings from the predefined lists
- Return the complete step result object matching the scheme

### prompts/FixStep.md

This prompt is loaded with `load vars`, so `%methods%` and `%!error%` get resolved.

The prompt should instruct the LLM to:
- Read the validation error from `%!error%`
- Read the previous result that failed validation
- Fix only what the validation error identifies
- Do not change fields that passed validation
- If the error cannot be fixed, return it in the errors array
- Return the complete step result object matching the same scheme as BuildStep
