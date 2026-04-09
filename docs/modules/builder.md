# Builder Module

Internal module used by the PLang build system. Not intended for direct use in `.goal` files — the builder goals (`system/builder/*.goal`) call these actions to parse, validate, merge, and save goals during the build process.

If you're writing PLang applications, you don't need this module. If you're working on the PLang build pipeline or writing a custom builder, read on.

## Actions

### actions

Returns metadata for all registered actions — module names, action names, parameter types, defaults, and cacheability. Used to generate the LLM prompt so the builder knows what actions are available.

```plang
- get all actions, write to %actions%
```

**Parameters:** None

**Returns:** Action metadata collection with parameter schemas for every registered module action.

### types

Returns PLang type names and JSON schemas for complex types. Used alongside `actions` to build the LLM prompt.

```plang
- get type info, write to %typeInfo%
```

**Parameters:** None

**Returns:** `BuilderTypeInfo` with `TypeNames` (comma-separated) and `TypeSchemas` (newline-separated complex type definitions).

### goals

Finds and parses `.goal` files from a directory. Merges existing `.pr` build data to preserve LLM-derived fields (actions, cache, error handling) across rebuilds.

```plang
- get goals from '.', write to %goals%
```

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| Path | string | no | "." | Directory to search for `.goal` files recursively |

**Returns:** `List<Goal>` — parsed goals with merged `.pr` data. File read errors appear as warnings, not failures.

### goals.save

Serializes goals to a `.pr` file. All goals from one `.goal` file share the same `PrPath` (derived from `Goal.Path`).

```plang
- save goals %goals%
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Goals | list | yes | Goals to serialize (must have Path set) |

**Returns:** `true` on success.

### actions.validate

Validates that LLM-returned actions exist in the module registry, resolves `goal.call` parameter paths, and fills default values.

```plang
- validate actions %actions%
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Actions | Actions | yes | Action collection to validate |

**Returns:** `true` if all actions are valid. Error listing unknown actions if any are not found.

### steps.merge

Merges LLM-derived fields from one step onto another. Structural fields (Text, Index, Indent, LineNumber) are preserved; LLM fields (Actions, Cache, OnError) are copied from the source.

```plang
- merge step %step% with %stepFromLlm%
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Step | Step | yes | Target step (structural data from parser) |
| StepFromLlm | Step | yes | Source step (LLM-derived data) |

**Returns:** The merged step.

### app

Loads application metadata from `.build/app.pr`.

```plang
- get app, write to %app%
```

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| Path | string | no | "." | Base directory containing `.build/app.pr` |

**Returns:** `AppData` if the file exists and is valid, `null` otherwise.

### app.save

Saves application metadata to `.build/app.pr`.

```plang
- save app %app%
```

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| App | AppData | yes | — | Application metadata to save |
| Path | string | no | ".build/app.pr" | Output file path |

**Returns:** The saved `AppData`.

## How the Build Pipeline Uses These Actions

The builder goals in `system/builder/` orchestrate these actions:

1. `builder.app` — load or create app metadata
2. `builder.goals` — parse `.goal` files, merge existing `.pr` data
3. `builder.actions` + `builder.types` — generate LLM context
4. LLM builds step actions
5. `builder.actions.validate` — verify actions exist, resolve paths, fill defaults
6. `builder.steps.merge` — merge LLM results into parsed steps
7. `builder.goals.save` — write `.pr` files
8. `builder.app.save` — update app metadata

## Key Concepts

### .goal File Format

```
GoalName
/ comment about the goal

- step text here
    - indented sub-step (4 spaces = 1 indent level)
- next step
  continuation line (indented, no dash = appends to previous step)


SecondGoal
/ this is a private sub-goal (not first in file)

- step in second goal
```

- First goal in a file is **Public**, the rest are **Private**
- Comments start with `/` (single-line) or `/* ... */` (multi-line)
- `//` is also a comment (extra `/` is retained)
- `\` at line start escapes the next character (for lines that would otherwise be parsed as comments or goal headers)
- Tabs are converted to 4 spaces before parsing

### Merge Semantics

When rebuilding, the builder preserves LLM work from previous builds:
- **Goal-level**: `Goal.MergeFrom()` matches steps by `Text` — unchanged steps keep their existing actions
- **Step-level**: `Step.Merge()` copies Actions, Cache, OnError, Errors, and Warnings from the source step
- Unmatched steps (new or changed text) keep empty Actions for the LLM to fill

### BuildingGuard

All builder actions check `engine.Building.IsEnabled` before executing. If building is not enabled, they return an error. This prevents accidental use of builder actions at runtime.
