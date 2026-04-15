# Debug Mode

PLang has a built-in debugger that dumps step execution info to stderr. It shows step text, actions, parameters, call stack, and variable values before and after each step.

All debug options are passed as JSON via `--debug={...}`. The JSON properties map directly to `Debug.@this` class properties.

Code: `PLang/App/Debug/this.cs`

## Usage

```bash
# Debug all steps in all goals
plang --debug

# Debug a specific goal
plang '--debug={"goal":"BuildGoal"}'

# Debug a specific step index within a goal
plang '--debug={"goal":"BuildGoal","step":3}'

# Watch specific variables (display at step boundaries)
plang '--debug={"variables":[{"name":"response"},{"name":"goal"}]}'

# Track variable mutations (logs every change with goal, step, type, stack trace)
plang '--debug={"variables":[{"name":"trace","event":"onchange"}]}'

# Set max line length (default 500)
plang '--debug={"maxLength":2000}'

# Filter output lines by regex
plang '--debug={"grep":"actions"}'

# Combine options
plang '--debug={"goal":"BuildGoal","step":3,"variables":["%actions%"],"maxLength":2000,"grep":"Module"}'
```

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `goal` | string | null | Filter to a specific goal name. Null = all goals. |
| `step` | int | null | Filter to a specific step index. Null = all steps. |
| `variables` | DebugVariable[] | null | Variables to watch. Each entry has `name` and optional `event`. |
| `maxLength` | int | 500 | Max characters per line before truncation. |
| `grep` | string | null | Regex pattern to filter output lines (case-insensitive). |
| `level` | string | "step" | Detail level: `"step"` (per step) or `"action"` (per action within steps). |

## Detail Levels

**step** (default): Shows BEFORE/AFTER for each step. Multi-action steps show the final state only.

**action**: Also shows BEFORE/AFTER for each action within a step. Useful for seeing how `%__data__%` flows between actions like `goal.call` → `variable.set`.

```bash
plang '--debug={"level":"action","variables":["%__data__%"]}'
```

## Output Format

All debug output goes to **stderr** (not stdout), so it doesn't interfere with program output.

### BEFORE Step

```
=== DEBUG [BEFORE]: Step [0] of GoalName ===
  Text: the step text
  Action: module.action
    ParamName = paramValue
  Call Stack:
    at Build.foreach (step 7) in /system/builder/Build.goal [243.8ms]
  Variables (2):
    %varName% = "value" (string)
    %other% = (undefined)
========================================
```

### AFTER Step

```
=== DEBUG [AFTER]: Step [0] of GoalName ===
  Variables (2):
    %varName% = "resolved value" (string)
    %other% = 42 (long)
========================================
```

### Goal Completed

```
--- DEBUG: Goal 'GoalName' completed ---
```

## Variable Display

- Strings: `"value"`
- Numbers: `42`
- Dictionaries: `{ key1: val1, key2: val2, ... } (N keys)` (first 3 entries)
- Lists: `[N items, first: preview]`
- Objects: `{ Prop1=val, Prop2=val }` (first 5 public properties)
- Undefined: `(undefined)` — variable not found in current scope
- Null: `(null)` — variable exists but value is null
- Each variable shows its type in parentheses: `(string)`, `(long)`, `(dict<string,object>)`
- Data properties are shown if present

## Combining with Build

```bash
# Build one file, debug the BuildGoal steps, watch a variable
plang build '--build={"files":"myfile.goal","cache":false}' '--debug={"goal":"BuildGoal","variables":["%actionSummary%"]}'
```

The `cache:false` option bypasses the LLM cache, forcing a fresh LLM call.

## Variable Watch

The `variables` property accepts objects with `name` and optional `event`:

```json
{"name": "trace", "event": "onchange"}
```

### Events

| Event | Description |
|-------|-------------|
| (none) | Display variable at step boundaries only (default) |
| `oncreate` | Log when the variable is first created in the store |
| `onchange` | Log every time the variable is replaced with a new value |
| `ondelete` | Log when the variable is removed from the store |

### How it works

The debugger creates placeholder Data objects in the variable store with event handlers attached. When the runtime creates or replaces the variable, events fire and the handler logs:
- Goal name and step index
- Old and new value types
- C# stack trace (top 5 frames)

Events are copied when a variable is replaced, so the handler survives across reassignments.

### Example: Track type mutations

```bash
plang build '--build={"files":"myfile.goal","cache":false}' \
  '--debug={"variables":[{"name":"trace","event":"onchange"}]}'
```

Output:
```
=== WATCH [trace] CHANGED ===
  Goal: BuildGoalCore[8] set %trace% = {"id": "%traceId%"...
  Type: null → String
  at this.FireOnChange:109
  at this.Set:71
  at Set.Run:52
==============================
```

### Example: Detect variable creation

```bash
plang '--debug={"variables":[{"name":"config","event":"oncreate"}]}'
```

Output:
```
=== WATCH [config] CREATED in Start[2] type=Dictionary`2 ===
```
