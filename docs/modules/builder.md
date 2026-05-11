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

Returns the structured action catalog — primitive type names, discovered record/enum entries, and pre-rendered `TypeNames` / `TypeSchemas` strings ready for the Liquid template. The catalog is what teaches the LLM the shape of every type a parameter can hold. See [Action Catalog](../../Documentation/v0.2/action-catalog.md) for the attribute model and rendering rules.

```plang
- get type info, write to %typeInfo%
```

**Parameters:** None

**Returns:** `Catalog` with `TypeNames` (comma-separated), `TypeSchemas` (newline-separated complex type definitions), and `Types` / `PrimitiveNames` for introspection.

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

Merges LLM-derived fields from one step onto another. Structural fields (Text, Index, Indent, LineNumber) are preserved; LLM fields (Actions — including each action's Modifiers, Errors, Warnings) are copied from the source.

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

### validateResponse

Validates the structural integrity of an LLM build response — step count matches the goal, indexes form `0..N-1` with no gaps, every step has at least one action, scalar-typed parameters carry plain string values rather than records. Errors are collected and returned together so the LLM-fixer pass can show them all to the LLM in one round trip.

**Parameters:** `StepResults` (the LLM's `BuildResponse`), `Goal` (the in-progress goal).

**Returns:** `Ok(true)` if structurally valid, `ValidationErrors` action error otherwise.

### enrichResponse

Backfills actions for `keep:true` steps from the prior `.pr` and tags each step with its source (`new` / `known` / `hint`). Run immediately after `validateResponse` succeeds, so the response carried into `merge` and `goals.save` has a full action graph.

**Parameters:** `StepResults`, `Goal`. **Returns:** the enriched `BuildResponse`.

### promoteGroups

Promotes grouped sub-steps into top-level steps so inline step handling renders correctly. Build-pipeline-only.

**Parameters:** `Steps`. **Returns:** the promoted step list.

### merge (step-level)

Build-pipeline counterpart to `steps.merge`. Folds an LLM-generated step result back onto the parser's step shape: copies `Actions` (with their `Modifiers`), `Errors`, and `Warnings` from the source step onto the target while preserving the target's structural fields (Text, Index, Indent, LineNumber).

**Parameters:** `Step` (target, from parser), `StepFromLlm` (source, from the LLM). **Returns:** the merged step.

## How the Build Pipeline Uses These Actions

The builder goals in `system/builder/` orchestrate these actions roughly in order:

1. `builder.app` — load or create app metadata
2. `builder.goals` — parse `.goal` files, merge existing `.pr` data
3. `builder.actions` + `builder.types` — generate LLM context
4. LLM builds step actions, returning a `BuildResponse`
5. `builder.validateResponse` — structural integrity check on the response
6. `builder.enrichResponse` — backfill `keep:true` steps and tag step source
7. `builder.actions.validate` — verify actions exist, resolve paths, fill defaults
8. `builder.promoteGroups` — promote grouped sub-steps into top-level steps
9. `builder.merge` / `builder.steps.merge` — fold LLM results back onto parsed steps
10. `builder.goals.save` — write `.pr` files (re-runs `validateResponse.ValidateGoalState` as a final safety net before persisting)
11. `builder.app.save` — update app metadata

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
- **Step-level**: `Step.Merge()` copies Actions, Errors, and Warnings from the source step. Each action's `Modifiers` (cache/timeout/error) travel inside Actions.
- Unmatched steps (new or changed text) keep empty Actions for the LLM to fill

### Build mode and runtime mode

Builder actions are **callable at runtime**, not just during `plang build`. There is no per-action `Builder.IsEnabled` guard; the file module's default `IFile` consults `App.Builder.IsEnabled` on the read path for snapshot logic, but the builder actions themselves run whenever a signed `.pr` calls them. In other words: the trust boundary is the goal signature, not a build/runtime mode flag. A signed third-party goal can invoke `builder.goals.save` at runtime and rewrite sibling `.pr` files; this is intentional (the user authorised the goal by trusting its signature) and consistent with PLang's user-sovereign threat model.

If you are reviewing or extending the builder pipeline, treat each builder action as a normal callable handler — there is no out-of-band gate above it.
