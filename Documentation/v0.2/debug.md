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
| `llm` | object | null | Granular LLM tracing — see [LLM Message Tracing](#llm-message-tracing). |
| `resolveTrace` | bool | false | Log every `%variable%` resolution with resolved type and depth. |

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
| `ontypechange` | Log only when the value's CLR type changes (e.g., Dictionary → String) |

### How it works

The debugger creates placeholder Data objects in the variable store with event handlers attached. When the runtime creates or replaces the variable, events fire and the handler logs:
- Goal name and step index
- Old and new value types
- C# stack trace (top 5 frames)

Event subscribers are **aliased** when a variable is replaced — `Variables.Set` shares the prev binding's `OnCreate`/`OnChange`/`OnDelete` list refs onto the new Data, so subscribers follow the *name* across any number of re-bindings. Subscribers added later (to either the prev ref or the current one) are visible from every alias because they share the same list.

### Example: Track type mutations

```bash
plang build '--build={"files":"myfile.goal","cache":false}' \
  '--debug={"variables":[{"name":"trace","event":"onchange"}]}'
```

Output:
```
=== WATCH [trace] CHANGED ===
  Goal: BuildGoalCore[8] set %trace% = {"id": "%traceId%"...
  Raw: null → Dictionary`2
  Resolved: null → Dictionary`2
  at this.FireOnChange:109
  at this.Set:71
  at Set.Run:52
==============================
```

The output shows both `Raw` (the stored `_value` field) and `Resolved` (what `.Value` returns after NeedsResolution). If Raw shows a type but Resolved shows null, the issue is in variable resolution. If Raw shows null, the Data itself has no value.

### Example: Detect variable creation

```bash
plang '--debug={"variables":[{"name":"config","event":"oncreate"}]}'
```

Output:
```
=== WATCH [config] CREATED in Start[2] type=Dictionary`2 ===
```

### Example: Catch type changes only

Use `ontypechange` to filter out noise — only fires when the CLR type of the value changes:

```bash
plang build '--build={"files":"myfile.goal","cache":false}' \
  '--debug={"variables":[{"name":"trace","event":"ontypechange"}]}'
```

Only fires for mutations like `null → Dictionary`, `Dictionary → String`, not `Dictionary → Dictionary`.

## LLM Message Tracing

The `llm` option is a record with one bool per slice of the API exchange. Set only the parts you want — each enabled flag emits its own labeled block to stderr.

| Sub-flag | What it dumps |
|----------|----------------|
| `system` | All system messages from the resolved request |
| `user` | All user messages from the resolved request |
| `schema` | The JSON Schema string passed via the format instruction |
| `response` | The raw response string returned by the LLM API |

Why granular: a full trace is too noisy when you're hunting a specific question. Asking "did the LLM emit X?" wants only `response`. Asking "is the catalog rendering correctly?" wants only `system`. Asking "is the schema permissive enough?" wants only `schema`.

```bash
# Just the response — most common when chasing "what did the LLM produce?"
plang build '--build={"cache":false}' '--debug={"llm":{"response":true}}'

# System prompt only — verify catalog/types render correctly. Bump maxLength.
plang build '--build={"cache":false}' '--debug={"llm":{"system":true},"maxLength":50000}'

# Schema only — when chasing type-fidelity bugs (e.g. permissive value?: object)
plang build '--build={"cache":false}' '--debug={"llm":{"schema":true}}'

# Combine flags freely
plang build '--build={"cache":false}' '--debug={"llm":{"system":true,"response":true},"maxLength":50000}'
```

Output (each block fires only when its flag is on):
```
=== LLM SYSTEM ===
# Goal Builder

You are the PLang compiler. Map each step in a goal to engine actions...
=== END LLM SYSTEM ===

=== LLM RESPONSE ===
{"description":"...","steps":[{"index":0,"actions":[{"module":"file","action":"read","parameters":[{"name":"Path","value":"test.txt","type":"path"}]}],...}]}
=== END LLM RESPONSE ===
```

Combine with `grep` to filter further:
```bash
# Only response lines mentioning a specific module
plang build '--debug={"llm":{"response":true},"grep":"file.read"}'
```

**Important — what `pass1.response` in trace files is NOT.** The `.build/traces/*.json` files capture an LLM call's `pass1.response` field *after* `builder.validateResponse` and `builder.enrichResponse` run, which means parameter values may have been normalized (e.g. a string `"data.txt"` for a `path`-typed parameter gets converted into a `Path` object whose every public property is then serialized into the .pr). Always use `llm.response` to see the actual API payload — don't infer LLM behaviour from the post-pipeline trace.

## Writing C# Diagnostic Lines (`context.App.Debug.Write`)

When you need a runtime diagnostic from inside C# (handlers, providers, data classes, builder pipeline), use the `Debug.Write` channel. **Never** use `System.IO.File.AppendAllText`, `Console.WriteLine`, or `Console.Error.WriteLine` for this — those bypass the `IsEnabled` gate, the `[Sensitive]` filter, and channel redirection.

```csharp
_ = context.App.Debug.Write($"merge: step.Index={step?.Index} step.Actions={step?.Actions.Count}");
```

Or `await` it at an async call site. Fire-and-forget (`_ =`) is fine when the diagnostic is best-effort.

**How it's wired:**
- `app.Debug.IsEnabled` is the gate. When `--debug` is off, `Write` returns immediately at zero cost. **Leave the calls in source** — they're free in production.
- When enabled, forwards to the `"debug"` channel — pre-registered to stderr; users can override by re-registering. Goes through the same serializer pipeline as other channel writes, so `[Sensitive]` properties are masked.

**Filter with `--debug={"grep":"..."}`:**
```bash
plang build '--debug={"grep":"merge:|goal.call"}'
```
Picks out only the diagnostic lines you care about, hiding the rest of the per-step BEFORE/AFTER blocks.

**What to write:**
- ✅ Type/shape at a boundary: `"value type={x.GetType().Name}, declared={schemaProp.PropertyType.Name}"`
- ✅ Count/key fields surviving a transform: `"after merge: actions={n}, modifiers={m}"`
- ❌ Stack traces (use `--debug={"verbose":true}` for that)
- ❌ Full payload dumps (use `--debug={"llm":{"response":true}}` for LLM data, or `--debug={"variables":[...]}` for vars)
- ❌ "I'm here" markers without context

## Resolve Tracing

Trace every `%variable%` resolution with the resolved type. Useful for understanding how values flow through the system.

```bash
plang build '--build={"files":"myfile.goal","cache":false}' \
  '--debug={"resolveTrace":true}'
```

Output:
```
  [ResolveDeep] %buildGoalPrompt% → String (depth=3)
  [ResolveDeep] %goalForLlm% → String (depth=3)
  [ResolveDeep] %traceId% → String (depth=2)
  [ResolveDeep] %goal.Name% → String (depth=2)
  [ResolveDeep] %Now% → DateTimeOffset (depth=2)
  [ResolveDeep] %stepResults% → Dictionary`2 (depth=2)
```

Each line shows:
- The variable name being resolved
- The CLR type it resolved to
- The nesting depth (how deep in the object tree the resolution is happening)
