# PLang Build Process & .pr File Format (v0.3)

## Overview

PLang has no parser. The builder uses an LLM to map natural language steps to typed actions. The result is a `.pr` file (JSON) that the runtime loads and executes directly.

```
.goal file → builder (LLM) → .pr file (JSON) → runtime execution
```

## .goal File Format

A `.goal` file contains one **root goal** (public) and zero or more **sub-goals** (private).

```plang
Start
- call goal WriteOut

WriteOut
- write out "hello plang world"
```

- The **first goal** is the root goal, visibility = Public
- All subsequent goals are sub-goals, visibility = Private
- Goals are separated by blank lines
- Steps start with `- `
- Comments start with `/` or `/* ... */`
- If steps appear before any goal header, an implicit `Start` goal is created

### Parsing: `Goal.Parse(text, path)`

`Goal.Parse` returns a single `Goal?` — the root goal. Sub-goals are nested in the root goal's `.Goals` property. It returns `null` for empty/whitespace-only files.

```
Input:  "Start\n- call goal WriteOut\n\nWriteOut\n- write out \"hello\""
Output: Goal { Name="Start", Goals=[Goal { Name="WriteOut" }] }
```

## .pr File Format (v0.3)

One `.pr` file per `.goal` file. The root goal is the JSON root, sub-goals are in the `goals` array.

**PrPath derivation**: `/folder/MyGoal.goal` → `/folder/.build/mygoal.pr`

```json
{
  "name": "Start",
  "description": "Calls WriteOut to display a greeting.",
  "steps": [
    {
      "index": 0,
      "text": "call goal WriteOut",
      "lineNumber": 2,
      "indent": 0,
      "actions": [
        {
          "module": "goal",
          "action": "call",
          "parameters": [
            {
              "name": "GoalName",
              "value": { "name": "WriteOut" },
              "type": "goal.call"
            }
          ]
        }
      ],
      "waitForExecution": true
    }
  ],
  "goals": [
    {
      "name": "WriteOut",
      "steps": [
        {
          "index": 0,
          "text": "write out \"hello plang world\"",
          "lineNumber": 5,
          "indent": 0,
          "actions": [
            {
              "module": "output",
              "action": "write",
              "parameters": [
                { "name": "Data", "value": "hello plang world", "type": "string" }
              ]
            }
          ],
          "waitForExecution": true
        }
      ],
      "visibility": 0,
      "path": "/Test.goal",
      "prPath": "/.build/test.pr"
    }
  ],
  "visibility": 1,
  "path": "/Test.goal",
  "prPath": "/.build/test.pr",
  "hash": "...",
  "builderVersion": "0.3",
  "isSetup": false,
  "isEvent": false,
  "isSystem": false,
  "isTest": false
}
```

### Goal Properties

| Property | Type | Description |
|----------|------|-------------|
| `name` | string | Goal name from the .goal file header |
| `description` | string? | LLM-generated summary of what the goal does |
| `comment` | string? | Developer comment (from `/` lines above the goal header) |
| `steps` | Step[] | The goal's steps (see below) |
| `goals` | Goal[] | Sub-goals (private goals from the same .goal file) |
| `visibility` | 0\|1 | 0 = Private, 1 = Public |
| `path` | string | Relative path to the source .goal file (e.g., `/Test.goal`) |
| `prPath` | string | Derived path to the .pr file (e.g., `/.build/test.pr`) |
| `hash` | string | SHA256 of goal name + step texts — used for change detection |
| `builderVersion` | string | Builder format version (currently `"0.3"`) |
| `isSetup` | bool | True if goal is named "Setup" or in `setup/` folder |
| `isEvent` | bool | True if this is an event goal |
| `isSystem` | bool | True if in `system/` folder |
| `isTest` | bool | True if from a `.test.goal` file |
| `inputParameters` | dict? | Named parameters the goal accepts |

### Step Properties

| Property | Type | Description |
|----------|------|-------------|
| `index` | int | Zero-based position within the goal |
| `text` | string | Original step text from the .goal file (without the `- ` prefix) |
| `lineNumber` | int | 1-based line number in the source .goal file |
| `indent` | int | Indent level (0 = top, 1 = one level in, etc.) |
| `comment` | string? | Comment from `/` line above this step |
| `actions` | Action[] | One or more actions mapped by the LLM |
| `onError` | OnError? | Error handling (retry, call goal, ignore) |
| `cache` | Cache? | Caching configuration |
| `waitForExecution` | bool | Whether to wait for completion (default true) |

### Action Properties

| Property | Type | Description |
|----------|------|-------------|
| `module` | string | Module namespace (e.g., `output`, `file`, `goal`, `condition`, `variable`) |
| `action` | string | Action class name (e.g., `write`, `read`, `call`, `if`, `set`) |
| `parameters` | Parameter[]? | List of `{name, value, type}` matching the action's properties |
| `return` | Return[]? | Variables to store results in: `[{name: "varName"}]` |
| `defaults` | Default[]? | Default parameter values added during validation |

### Parameter Properties

| Property | Type | Description |
|----------|------|-------------|
| `name` | string | Property name on the action class |
| `value` | any | The value — can be string, number, bool, object, or `%variable%` reference |
| `type` | string? | Type hint: `string`, `int`, `long`, `bool`, `path`, `goal.call`, etc. |

## Build Process

### Entry Point

`system/Build.goal` → calls `system/builder/Build.goal`

### Build Flow

```
Build.goal
  1. Set default path = "."
  2. GetApp — loads/creates app.pr (app identity)
  3. GetGoalsV2 — find all .goal files, parse each into a root Goal
  4. foreach goal → call BuildGoal
  5. SaveApp — saves app.pr
```

### BuildGoal (single LLM pass per goal)

```
BuildGoal.goal
  1. Get available actions (module registry)
  2. Get type info (valid PLang types)
  3. Render goal as template for LLM context
  4. Send to LLM with BuildGoal.llm prompt
     - LLM sees all steps, returns {module, action, parameters} per step
     - Also returns confidence level: high/medium/low
  5. foreach step result → call ApplyStep
  6. Set goal.BuilderVersion = "0.3"
  7. SaveGoal — serialize to .pr file
```

### ApplyStep (validate + merge)

```
ApplyStep.goal
  1. ValidateActions — check module/action exists, resolve goal.call paths, normalize types
  2. MergeStep — merge LLM result onto the parsed step
  3. If confidence != high → call BuildStep (detail pass)
```

### BuildStep (detail pass, only for medium/low confidence)

```
BuildStep.goal
  1. Render step with full action details (property definitions, types)
  2. Send to LLM with BuildStep.llm prompt
  3. Validate result
  4. Update step actions
```

### Two-Pass Design

The builder uses two LLM passes:

1. **BuildGoal** (broad pass) — sees all steps in a goal at once. Maps each to module/action with parameters. Fast, but may not have full detail for complex steps.
2. **BuildStep** (detail pass) — only runs for steps where BuildGoal was uncertain (medium/low confidence). Gets full action property definitions. Slower but precise.

Most steps are fully built in pass 1. Pass 2 is the fallback.

### GoalsSave

`GoalsSave` serializes the root goal (with sub-goals in `.Goals`) to the .pr path. One .pr file per .goal file.

## Runtime Loading

At runtime, the engine loads .pr files on demand:

1. `Goals.GetAsync(name)` — checks cache, then tries to load from disk
2. `TryLoadPr(dir, file)` — looks for `{dir}/.build/{file}.pr`
3. `LoadFromFileAsync` — deserializes JSON, supports both single goal object and array of goals
4. Sub-goals from `.goals` array are registered alongside the root goal
5. `Step.Goal` back-references are wired up after deserialization

### Path Resolution

- Names starting with `/` resolve from app root
- Relative names resolve from the calling goal's folder first, then app root
- System goals (in `system/` folder) resolve from the system directory

## Key Files

| File | Purpose |
|------|---------|
| `system/Build.goal` | Build entry point |
| `system/builder/Build.goal` | Main build orchestration |
| `system/builder/BuildGoal.goal` | Per-goal LLM build |
| `system/builder/BuildStep.goal` | Detail pass for uncertain steps |
| `system/builder/ApplyStep.goal` | Validate + merge step results |
| `system/builder/llm/BuildGoal.llm` | LLM prompt for the broad pass |
| `system/builder/llm/BuildStep.llm` | LLM prompt for the detail pass |
| `PLang/App/Goals/Goal/this.cs` | Goal entity, Parse(), PrPath derivation |
| `PLang/App/Goals/this.cs` | Goal collection, loading, caching |
| `PLang/App/modules/builder/providers/DefaultBuilderProvider.cs` | Builder actions (goals, save, validate, merge) |
