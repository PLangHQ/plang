# Debug Mode

PLang has a built-in debugger that dumps step execution info to stderr. It shows step text, actions, parameters, call stack, and variable values before and after each step.

Code: `PLang/App/Debug/this.cs`

## Enabling Debug

```bash
# Debug all steps in all goals
plang --debug

# Debug a specific goal
plang --debug=GoalName
plang '--debug={"goal":"BuildGoal"}'

# Debug a specific step index within a goal
plang '--debug={"goal":"BuildGoal","step":3}'

# Debug during build
plang build --debug
plang build '--build={"files":"myfile.goal"}' '--debug={"goal":"BuildStep"}'
```

## Watching Variables

By default, debug only shows variables referenced in the current step's action parameters. To watch specific variables across all steps:

```bash
plang '--debug={"variables":["%response%","%goal%"]}'
```

Variables are specified with or without `%` signs. They appear in every BEFORE/AFTER output alongside the step's own variables.

## Filtering Output

### Max Line Length

Debug output truncates long lines. Default is 500 characters. Override via:

```bash
plang --debug '--debug.maxLength=2000'
```

Truncated lines show the total character count: `...({length} chars)`

### Grep Filter

Filter debug output to lines matching a regex pattern:

```bash
plang --debug '--debug.grep=actions'
```

Case-insensitive. If the pattern is not valid regex, it's treated as a literal string match.

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

## How It Works

Debug mode registers event handlers on `BeforeStep`, `AfterStep`, and `AfterGoal` lifecycle events. These handlers inspect the current context's step, actions, and variables, then write formatted output to stderr.

The event handlers use:
- `goalNamePattern` — wildcard filter on goal name (`"*"` for all, or specific goal name)
- `stepFilter` — optional step index filter
- `WatchVariables` — additional variables to display beyond those referenced in step parameters

## Combining with Build

```bash
# Build one file, debug the BuildGoal steps, watch a variable
plang build '--build={"files":"myfile.goal","cache":false}' '--debug={"goal":"BuildGoal","variables":["%actionSummary%"]}'
```

The `cache:false` option bypasses the LLM cache, forcing a fresh LLM call. Useful when debugging builder prompt changes.
