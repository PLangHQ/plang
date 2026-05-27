# PLang Build Process & .pr File Format (v0.2)

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

## .pr File Format (v0.2)

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
  "builderVersion": "0.2",
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
| `builderVersion` | string | Builder format version (currently `"0.2"`) |
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
| `waitForExecution` | bool | Whether to wait for completion (default true) |

Error handling, caching, and timeouts are per-action — see the `modifiers` field on Action below.

### Action Properties

| Property | Type | Description |
|----------|------|-------------|
| `module` | string | Module namespace (e.g., `output`, `file`, `goal`, `condition`, `variable`) |
| `action` | string | Action class name (e.g., `write`, `read`, `call`, `if`, `set`) |
| `parameters` | Parameter[]? | List of `{name, value, type}` matching the action's properties |
| `return` | Return[]? | Variables to store results in: `[{name: "varName"}]` |
| `defaults` | Default[]? | Default parameter values added during validation |
| `modifiers` | Action[]? | Modifier actions (cache/timeout/error) folded around this action at runtime. Same shape as an action; grouped by the builder and pre-sorted by `[Modifier(Order)]`. |

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

### BuildGoal (one planner pass per goal, then per-step compile)

```
BuildGoal/Start.goal
  1. Call Plan (one LLM pass per goal — the planner)
  2. foreach plan.steps → call BuildStep/Start
  3. foreach sub-goals → call BuildSubGoal (recurses)
  4. Save trace, builder.goalsSave Goal=%goal%
```

### Plan (planner — one pass per goal)

```
BuildGoal/Plan.goal
  1. Render summary.planner.md template → %actionSummary%
  2. Render goalFormatForLlm.v2.template → %goalForLlm%
  3. Render Plan.llm template → planner system prompt
  4. llm.query with the planner prompt
     - Schema: {description, steps: [{index, actions: [string]}]}
     - LLM returns the set of "module.action" tokens involved per step (unordered)
```

### BuildStep (compiler — one pass per step)

```
BuildStep/Start.goal
  1. builder.validateStepActions — drop hallucinated entries, append literal mentions
  2. builder.actions — load full schemas for the planner's picked actions
  3. builder.types — primitive types + referenced catalog types
  4. goal.getTypes — variable types in scope at this step (incremental)
  5. Render Compile.llm (system) + CompileUser.llm (user) templates
  6. llm.query — compiler decides chain order, modifier nesting, parameter values
  7. builder.validate the compiled actions; FixValidation retries on failure
```

### Planner / compiler split

The builder uses two LLM calls per goal-build, with very different roles:

1. **Planner** (`Plan.llm`) — runs ONCE per goal. Sees all steps; for each step returns the **set of actions** the step uses (unordered `module.action` tokens). Cheap and broad.
2. **Compiler** (`Compile.llm` + `CompileUser.llm`) — runs ONCE per step. Sees only that step's text plus the planner's action set, and emits the structured `actions[]` array (chain order, modifier nesting, parameter values). The per-action Notes/Examples that constrain compile shape live in `os/system/modules/<m>/<action>.{notes,examples,description}.md` and are rendered only for the planner's picked actions, keeping the system prompt stable for provider-side cache.

### GoalsSave

`GoalsSave` serializes the root goal (with sub-goals in `.Goals`) to the .pr path. One .pr file per .goal file. Before serialization, each step calls `step.Actions.GroupModifiers(app.Modules)` — this takes the flat LLM-produced action list and attaches every `[Modifier]` action onto the preceding executable action's `Modifiers` collection, sorted by `[Modifier(Order)]`. Runtime never sorts or classifies: the `.pr` file is already the execution plan.

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
| `system/builder/BuildGoal/Start.goal` | Per-goal pipeline: planner + per-step compile + sub-goal recursion |
| `system/builder/BuildGoal/Plan.goal` | Planner — one LLM call per goal |
| `system/builder/BuildStep/Start.goal` | Compiler — one LLM call per step |
| `system/builder/BuildStep/Validate.goal` | Post-compile validation; FixValidation retry |
| `system/builder/llm/Plan.llm` | Planner system prompt |
| `system/builder/llm/Compile.llm` | Compiler system prompt (stable across steps for prompt cache) |
| `system/builder/llm/CompileUser.llm` | Compiler user-message template (per-step action detail + step text) |
| `os/system/modules/<m>/<action>.{notes,examples,description}.md` | Per-action LLM teaching, rendered only when the action is in the planner's set |
| `PLang/app/goals/goal/this.cs` | Goal entity, Parse(), PrPath derivation |
| `PLang/app/goals/this.cs` | Goal collection, loading, caching |
| `PLang/app/modules/builder/code/Default.cs` | Builder actions (goals, save, validate, merge) |
