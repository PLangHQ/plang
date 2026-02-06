# PLang Builder — Design Conversation Digest

This document captures the full design conversation for the PLang builder. Every decision, correction, and rationale is recorded here in chronological order.

---

## 1. Starting Point: Object-Based Architecture Context

The builder sits on top of the PLang Runtime, which uses an object-based architecture. Key Runtime principles that affect the builder:

- Modules expose a single entry point: `Execute(string method, object? data)`
- Every module method accepts **one object** — two parameters are not allowed
- The `data` parameter is typed: `{ type: "db.selectData", data: { ... } }`
- The Runtime resolves `%variable%` syntax from MemoryStack at execution time
- PLang handles type conversion automatically — no manual serialization
- `.pr` files are the compiled JSON format that the Runtime executes

## 2. Builder's Purpose

Transform `.goal` files (natural language) into `.pr` files (JSON the Runtime executes).

The builder should be **minimal in C#** and **mostly written in PLang**. C# provides infrastructure methods. PLang + LLM calls handle the actual build logic.

## 3. C# Surface Area — Six Methods

The C# layer exposes these methods as PLang module calls:

### `get plang app, parser:"app"`
- Loads or creates the app-level metadata object
- If `/.build/app.pr` exists, loads it (preserving `Id` and `Created`)
- If it doesn't exist, creates a new one with a fresh `Id` and current timestamp
- Returns `{ id: string, created: datetime, updated: datetime }`
- Saved back via `save plang %app% to .build/app.pr` after build completes

### `get all goals, parser:"goal"`
- Parses all `.goal` files in the project into Goal/Step objects
- Sets **deterministic properties only** — these must never be modified by the LLM:
  - `Text` — the original natural language step text
  - `Created` — timestamp
  - `Update` — timestamp
  - `Hash` — hash of the step text for change detection
  - `Index` — position in the goal
  - `LineNumber` — line number in the `.goal` file

### `get plang modules, format:"md"`
- Returns all registered modules as a markdown document
- Used by the LLM to select which module handles a step

### `get plang methods for %module%, format:"md"`
- Returns a specific module's methods as markdown
- Includes method signatures, data types, and type definitions
- Type definitions include shared types like `ErrorHandler`, `CacheSettings`, `GoalToCallInfo`
- The LLM uses these type definitions to produce correctly typed JSON

### `save plang %goal% to %goal.PrPath%`
- Serializes a fully built Goal object to `.pr` JSON format

### `save plang %app% to .build/app.pr`
- Serializes the App object to JSON, sets `Updated` to current timestamp

### `validate %module%, %method%, %data%`
- C# validation method that checks the LLM's output against the actual module
- Verifies the module exists, the method exists on that module, and the data object matches the method's expected parameter type
- Returns success or throws an error accessible via `%!error%`
- This is structural validation only — does the output conform to what the module expects

## 4. Build Flow — Two LLM Passes

### Pass 1: Module Selection (per goal)

The LLM sees the **entire goal file** at once — all steps together. This gives it context about the overall flow. It assigns a module to each step and optionally writes an `Intent` — the LLM's understanding of the user's intent for that step.

```plang
Build
- get plang app, parser:"app", write to %app%
- get all goals, parser:"goal", write to %goals%
- foreach %goals% call BuildGoal, use 80% cpu
- save plang %app% to .build/app.pr

BuildGoal
- get plang modules, format:"md", write to %modules%
- read file "prompts/SelectModules.md", load vars, write to %selectModulesPrompt%
- [llm] system: %selectModulesPrompt%
    user: %goal%
    scheme: [{stepIndex: int, module: string, intent: string?}]
    write to %moduleAssignments%
- foreach %moduleAssignments% call AssignModule
- foreach %goal.Steps% call BuildStep, use 80% cpu
- save plang %goal% to %goal.PrPath%
```

Doing module selection on the whole goal at once means the LLM understands the flow. If step 3 says `- write out %user%`, the LLM knows from step 2 that `%user%` came from a db query, which helps it assign the correct module.

The `intent` field is the LLM's interpretation of the user's purpose. It provides flow-of-thinking from module selection to step building. Example: "This step fetches the user record needed for validation in the next step."

### Pass 2: Step Building (per step, parallelized)

For each step, the LLM sees the step text, the intent from Pass 1, and the available methods for the assigned module. It produces the method, typed data object, return variable, execution policies, and any build errors/warnings.

After the LLM produces its result, C# validates that the module, method, and data object are structurally correct. If validation fails, the LLM gets one retry with the error message. If the retry also fails, the error is recorded on the step and the builder moves on.

```plang
BuildStep
- if %step.Hash% equals %step.PreviousHash% then
    - end goal
- get plang methods for %step.Module%, format:"md", write to %methods%
- read file "prompts/BuildStep.md", load vars, write to %buildStepPrompt%
- [llm] system: %buildStepPrompt%
    user: Step: %step.Text%
    Intent: %step.Intent%
    scheme: { method: string, data: {type: string, data: object}, return?: string, onError?: ErrorHandler, cache?: CacheSettings, cancelAfterSeconds?: int, errors: [string], warnings: [string] }
    write to %stepResult%
- validate %step.Module%, %stepResult.method%, %stepResult.data%
    on error call RetryBuildStep
- set %step.Method% = %stepResult.method%
- set %step.Data% = %stepResult.data%
- set %step.Return% = %stepResult.return%
- set %step.OnError% = %stepResult.onError%
- set %step.Cache% = %stepResult.cache%
- set %step.CancelAfterSeconds% = %stepResult.cancelAfterSeconds%
- set %step.Errors% = %stepResult.errors%
- set %step.Warnings% = %stepResult.warnings%

RetryBuildStep
- read file "prompts/FixStep.md", load vars, write to %fixStepPrompt%
- [llm] system: %fixStepPrompt%
    user: Step: %step.Text%
    Intent: %step.Intent%
    Previous result: %stepResult%
    scheme: { method: string, data: {type: string, data: object}, return?: string, onError?: ErrorHandler, cache?: CacheSettings, cancelAfterSeconds?: int, errors: [string], warnings: [string] }
    write to %stepResult%
- validate %step.Module%, %stepResult.method%, %stepResult.data%
    on error call HandleBuildStepFailure

HandleBuildStepFailure
- set %step.Errors% = [%!error%]
- end goal and previous 2
```

The call stack when the second validation fails:

```
BuildStep → RetryBuildStep → HandleBuildStepFailure
                                 ↓
                          set error on step
                          end goal and previous 2
                          (exits HandleBuildStepFailure + RetryBuildStep + BuildStep)
```

`end goal and previous 2` pops three frames, so execution returns to the `foreach` in BuildGoal which moves to the next step. The failed step has its error recorded. The goal still gets saved with whatever steps succeeded.

## 5. Change Detection

Only steps whose text has changed since the last build are rebuilt:

```plang
- if %step.Hash% equals %step.PreviousHash% then
    - end goal
```

If one step in a goal changes, only that step is rebuilt. Unchanged steps are skipped.

## 6. The Step Object — Complete Property List

### Deterministic Properties (set by parser, never modified by LLM)

| Property | Type | Description |
|----------|------|-------------|
| Text | string | Original natural language step text |
| Created | datetime | When the step was first created |
| Update | datetime | Last modification time |
| Hash | string | Hash of step text for change detection |
| Index | int | Position within the goal |
| LineNumber | int | Line number in the `.goal` file |

### LLM Pass 1 Properties (set by module selection)

| Property | Type | Description |
|----------|------|-------------|
| Module | string | Which module handles this step (e.g., "db", "http", "io") |
| Intent | string? | LLM's understanding of the user's intent for this step |

### LLM Pass 2 Properties (set by step builder)

| Property | Type | Description |
|----------|------|-------------|
| Method | string | Which method on the module (e.g., "select", "insert") |
| Data | object | `{ type: string, data: object }` — typed request object |
| Return | string? | Variable name from "write to %variable%" |
| OnError | ErrorHandler? | Error handling + retry configuration |
| Cache | CacheSettings? | Caching configuration |
| CancelAfterSeconds | int? | Timeout / cancellation |
| Errors | List\<string\> | Build errors (from LLM or C#) |
| Warnings | List\<string\> | Build warnings (from LLM or C#) |

### Goal-Level Properties

| Property | Type | Description |
|----------|------|-------------|
| Errors | List\<string\> | Goal-level build errors |
| Warnings | List\<string\> | Goal-level build warnings |

## 7. The `data` Field — Typed Request Objects

All module methods accept one object. The `data` field in the `.pr` file has a `type` that tells the module what to expect, and a `data` field with the actual content.

Convention: `moduleName.dataTypeName`

### Database Example

The LLM generates actual SQL — no magic mapping from JSON to SQL:

```json
{
    "type": "db.select",
    "data": { "sql": "select * from users", "parameters": null }
}
```

```json
{
    "type": "db.insert",
    "data": {
        "sql": "insert into users (name, email) values (@name, @email)",
        "parameters": [{"@name": "%name%"}, {"@email": "%email%"}]
    }
}
```

Database data types:

```
db.select:  { sql: string(required), parameters?: [{key: value}] = null }
db.insert:  { sql: string(required), parameters?: [{key: value}] = null }
db.update:  { sql: string(required), parameters?: [{key: value}] = null }
db.delete:  { sql: string(required), parameters?: [{key: value}] = null }
```

## 8. Execution Policies on Steps

Steps can carry execution policies parsed by the LLM from indented lines beneath the step text.

### Error Handling

Four variations:

```plang
/ retry first, then call goal if all retries fail
- do something
    on error retry 2 times for 30 sec, then call ErrorHandler

/ call goal first, then retry
- do something
    on error call HandleError then retry 3 times over 10 seconds

/ retry only, no goal
- do something
    on error retry 3 times over 10 seconds

/ goal only, no retry
- do something
    on error call HandleError
```

The error handler goal can receive parameters:

```plang
- on error call HandleError name=%ble.blu%
```

Errors are always accessible through `%!error%`.

### Caching

```plang
- select * from users
    cache for 10 min from last usage
```

- "from last usage" = sliding expiration (resets timer on each access)
- "from first usage" = absolute expiration
- Key is optional — if not provided, the LLM generates one

### Cancellation

```plang
- select * from users
    cancel after 30 sec
```

Wraps the module Execute call with a `CancellationTokenSource` timeout.

### Combined Example

```plang
- select * from users
    on error call HandleError then retry 3 times over 10 seconds
    cache for 10 min from last usage
    cancel after 30 sec
    write to %users%
```

## 9. Type Definitions for Execution Policies

### ErrorHandler

```csharp
public class ErrorHandler
{
    public GoalToCallInfo? Goal { get; set; }
    public int? RetryCount { get; set; }
    public int? RetryOverSeconds { get; set; }
    public ErrorOrder? Order { get; set; }
}

public enum ErrorOrder { GoalFirst, RetryFirst }
```

### GoalToCallInfo

Used anywhere PLang references a goal — error handlers, channel bindings, event handlers, `call goal` steps:

```csharp
public class GoalToCallInfo
{
    public string Name { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
}
```

### CacheSettings

```csharp
public class CacheSettings
{
    public int DurationMinutes { get; set; }
    public bool Sliding { get; set; }
    public string? Key { get; set; }
}
```

## 10. Build Errors and Warnings

Both `Goal` and `Step` have `Errors` and `Warnings` as `List<string>`. These can be populated by both the LLM at build time and C# at runtime.

The LLM has predefined error/warning keys to choose from (fixed enums):

### Build Errors (LLM picks from these)
- `MissingVariable` — step references a variable that hasn't been set
- `ModuleNotFound` — no module matches the step
- `MethodNotFound` — module found but no method matches
- `InvalidParameter` — parameter doesn't match method signature

### Build Warnings (LLM picks from these)
- `AmbiguousModule` — LLM wasn't confident about module selection
- `AmbiguousMethod` — LLM wasn't confident about method selection
- `MissingErrorHandler` — step calls external service with no `on error`
- `NoWriteTo` — step produces data but doesn't capture it
- `UnusedVariable` — writes to a variable never read

## 11. Validation and Retry

After the LLM builds a step, C# validates the result structurally:

```plang
- validate %step.Module%, %stepResult.method%, %stepResult.data%
```

This calls a C# method that:
1. Checks the module exists in the registry
2. Checks the method exists on that module
3. Checks the `data` object matches the method's expected parameter type

If validation fails, it throws an error accessible via `%!error%`. The builder calls `RetryBuildStep` which sends the error back to the LLM via the `FixStep.md` prompt. The LLM gets one chance to fix its output. If the second validation also fails, the error is recorded on the step and the builder moves to the next step.

This prevents invalid `.pr` files from being saved — the Runtime won't encounter structurally broken step data.

## 12. System Prompts from Files

System prompts are read from files, not inlined. Variables in prompt files are resolved at runtime using `load vars`:

```plang
- read file "prompts/SelectModules.md", load vars, write to %selectModulesPrompt%
```

The `SelectModules.md` file contains `%modules%` which gets resolved to the modules markdown before being passed to the LLM.

Prompt files:

```
prompts/
├── SelectModules.md      ← module selection prompt, references %modules%
├── BuildStep.md          ← step building prompt, references %methods%
└── FixStep.md            ← retry prompt, references %methods% and %!error%
```

The prompts are structured and strict, with PLang syntax examples. Each module provides its own examples via `get plang modules` and each method provides examples via `get plang methods` — these are already in markdown format and get embedded in the prompt via `%modules%` and `%methods%` variables.

This makes prompts editable and versionable without rebuilding the builder.

## 13. .pr File Format

Complete example of a built step in the `.pr` JSON:

```json
{
    "path": "/CreateUser",
    "steps": [
        {
            "text": "select * from users where id=@id",
            "created": "2025-01-01T00:00:00Z",
            "update": "2025-02-01T00:00:00Z",
            "hash": "abc123",
            "index": 0,
            "lineNumber": 2,
            "module": "db",
            "intent": "Fetch user by ID for validation before creating a new record",
            "method": "select",
            "data": {
                "type": "db.select",
                "data": {
                    "sql": "select * from users where id=@id",
                    "parameters": [{"@id": "%userId%"}]
                }
            },
            "return": "%existingUser%",
            "onError": {
                "goal": { "name": "HandleDbError", "parameters": null },
                "retryCount": 3,
                "retryOverSeconds": 10,
                "order": "RetryFirst"
            },
            "cache": {
                "durationMinutes": 10,
                "sliding": true,
                "key": "user_by_id_%userId%"
            },
            "cancelAfterSeconds": 30,
            "errors": [],
            "warnings": []
        }
    ],
    "errors": [],
    "warnings": []
}
```

## 14. Bootstrap

The builder is written in PLang, but to run PLang you need `.pr` files. The builder's own `.goal` files need to be hand-compiled into `.pr` files once. After that, the builder can build everything else, including future versions of itself.

Files that need hand-built `.pr` files:

- `Build.goal`
- `BuildGoal.goal`
- `AssignModule.goal`
- `BuildStep.goal`
- `RetryBuildStep.goal`
- `HandleBuildStepFailure.goal`

## 15. Complete PLang Builder Code

```plang
Build
- get plang app, parser:"app", write to %app%
- get all goals, parser:"goal", write to %goals%
- foreach %goals% call BuildGoal, use 80% cpu
- save plang %app% to .build/app.pr

BuildGoal
- get plang modules, format:"md", write to %modules%
- read file "prompts/SelectModules.md", load vars, write to %selectModulesPrompt%
- [llm] system: %selectModulesPrompt%
    user: %goal%
    scheme: [{stepIndex: int, module: string, intent: string?}]
    write to %moduleAssignments%
- foreach %moduleAssignments% call AssignModule
- foreach %goal.Steps% call BuildStep, use 80% cpu
- save plang %goal% to %goal.PrPath%

AssignModule
- set %goal.Steps[moduleAssignment.stepIndex].Module% = %moduleAssignment.module%
- set %goal.Steps[moduleAssignment.stepIndex].Intent% = %moduleAssignment.intent%

BuildStep
- if %step.Hash% equals %step.PreviousHash% then
    - end goal
- get plang methods for %step.Module%, format:"md", write to %methods%
- read file "prompts/BuildStep.md", load vars, write to %buildStepPrompt%
- [llm] system: %buildStepPrompt%
    user: Step: %step.Text%
    Intent: %step.Intent%
    scheme: { method: string, data: {type: string, data: object}, return?: string, onError?: ErrorHandler, cache?: CacheSettings, cancelAfterSeconds?: int, errors: [string], warnings: [string] }
    write to %stepResult%
- validate %step.Module%, %stepResult.method%, %stepResult.data%
    on error call RetryBuildStep
- set %step.Method% = %stepResult.method%
- set %step.Data% = %stepResult.data%
- set %step.Return% = %stepResult.return%
- set %step.OnError% = %stepResult.onError%
- set %step.Cache% = %stepResult.cache%
- set %step.CancelAfterSeconds% = %stepResult.cancelAfterSeconds%
- set %step.Errors% = %stepResult.errors%
- set %step.Warnings% = %stepResult.warnings%

RetryBuildStep
- read file "prompts/FixStep.md", load vars, write to %fixStepPrompt%
- [llm] system: %fixStepPrompt%
    user: Step: %step.Text%
    Intent: %step.Intent%
    Previous result: %stepResult%
    scheme: { method: string, data: {type: string, data: object}, return?: string, onError?: ErrorHandler, cache?: CacheSettings, cancelAfterSeconds?: int, errors: [string], warnings: [string] }
    write to %stepResult%
- validate %step.Module%, %stepResult.method%, %stepResult.data%
    on error call HandleBuildStepFailure

HandleBuildStepFailure
- set %step.Errors% = [%!error%]
- end goal and previous 2
```
