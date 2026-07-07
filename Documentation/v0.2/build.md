# Build Mode

PLang transforms `.goal` files (natural language) into `.pr` files (JSON) using an LLM. Build mode is activated via `plang build` or `--build`.

All build options are passed as JSON via `--build={...}`. The JSON properties map directly to `Builder.@this` class properties.

Code: `PLang/app/module/builder/this.cs`

> **Rebuilding the builder itself?** Go straight to [building-the-builder.md](building-the-builder.md) — the bootstrap case has specific cwd and file-order requirements that nothing here covers.

## Usage

```bash
# Build all .goal files in current directory
plang build

# Build a specific file
plang build '--build={"files":"myfile.goal"}'

# Build multiple files
plang build '--build={"files":["file1.goal","file2.goal"]}'

# Build without LLM cache (forces fresh LLM call)
plang build '--build={"cache":false}'

# Combine options
plang build '--build={"files":"myfile.goal","cache":false}'
```

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `files` | string or string[] | (all) | File filter. Single string or array of filenames. Only matching .goal files are built. |
| `cache` | bool | true | Whether to use the LLM response cache. Set to false to force a fresh LLM call. |

## How It Works

1. `Build.goal` (system builder) is the entry point
2. For each `.goal` file, `BuildGoal/Start.goal` runs the planner (`Plan.llm`, one LLM call per goal) to decide each step's action set
3. For each step, `BuildStep/Start.goal` runs the compiler (`Compile.llm` + `CompileUser.llm`) with the planner's picked actions and emits the structured `{module, action, parameters}` mapping
4. The result is validated, merged, and saved as a `.pr` file (one file per goal in v0.2)

## Cache

LLM responses are cached in `.db/system.sqlite`. When building the same goal with the same content, the cached response is used (fast, ~0.4s). With `cache:false`, the cache is bypassed and the LLM is called fresh (~2-4s).

The cache key is based on the goal content hash. If you change the `.goal` file, the cache is automatically invalidated.

## Combining with Debug

```bash
# Build one file, debug the builder, watch variables
plang build '--build={"files":"myfile.goal","cache":false}' '--debug={"goal":"BuildGoal","variables":[{"name":"%actionSummary%"},{"name":"%goalForLlm%"}]}'
```

See [debug.md](debug.md) for full debug documentation.

## Diagnosing "why didn't the planner pick my action?"

Symptom: you added a new action to the catalog (C# handler + `os/system/modules/<module>/<action>.description.md`), wrote a step that uses it, built — and the step compiled to something else (often the closest existing action). No error; just wrong behavior.

Two signals to check, in order:

### 1. Confidence on the per-step output

The builder emits per-step `confidence` (`VeryHigh|High|Medium|Low|VeryLow`) from both the planner AND the compiler. `Low` / `VeryLow` warnings surface in the build output as:

```
  [✓] compress %original%, write to %archived%
      ⚠ planner VeryLow: No action matches verb 'compress'.
      ⚠ compiler VeryLow: The step text requires a 'compress' operation, but the provided action set contains only variable.set.
```

If you see those, the planner/compiler is telling you it had to settle for a fallback — usually because your new action wasn't visible to it.

### 2. See what the planner actually saw

To verify the planner LLM was given your new action in its catalog, use `--debug` with `llmTrace`:

```bash
plang '--build={"files":"myfile.goal","cache":false}' '--debug={"llmTrace":true,"maxLength":50000}'
```

That prints `=== LLM REQUEST ===` blocks for each `llm.query` call, including the planner's system prompt with the full `{{ actionSummary }}` block. Search the request body for your action name — if it's missing there, the catalog discovery didn't pick it up. Common causes:

- `.description.md` filename doesn't match the action name (must be `<actionName>.description.md`)
- File saved to the wrong folder — must be under `os/system/modules/<moduleName>/`
- C# handler not registered (verify it appears in `Modules.Describe()` output)

`pass1.response` in the trace files at `.build/traces/` is captured AFTER `builder.validateResponse` + `builder.enrichResponse` run — it's post-pipeline state, not the raw LLM emission. For diagnosing "what the LLM actually returned," use `llmTrace`, not the trace files.

## Confidence per step

All four LLM passes inside the builder — planner (`Plan.llm`), compiler
(`Compile.llm`), and the two recovery prompts (`RefineActions`,
`FixValidation`) — emit a `confidence` field with their result:

| Level | Meaning |
|---|---|
| `VeryHigh` | The model is certain about every action and every parameter. |
| `High` | Confident on the verb; minor uncertainty on a single parameter or shape. |
| `Medium` | Picked one of several plausible actions / shapes; reader should verify. |
| `Low` | Settled for a fallback. An action the step text implied may be missing from the catalog, or the parameter mapping is ambiguous. |
| `VeryLow` | No good match for the verb / shape — the result is almost certainly wrong. |

`Medium` and below also carry an `explanation` string. `Low` / `VeryLow`
surface in build output as warning lines beneath the offending step:

```
  [✓] compress %original%, write to %archived%
      ⚠ planner VeryLow: No action matches verb 'compress'.
      ⚠ compiler VeryLow: The step text requires a 'compress' operation, but the provided action set contains only variable.set.
```

Both numbers are also persisted to the trace file at
`.build/traces/<run>/<goal>.json` under `plan.steps[].confidence` and
`stepPasses[].value.response.confidence` — useful for asserting against
in tests, though there's currently no automated test that does so.

`VeryHigh` / `High` are silent. They are not always right (the LLM can
be confidently wrong), but they are the noise floor.

## Builder output routing

Build-time output does not call `Console.WriteLine` and does not write
directly to `"output"`. The builder registers its own named channel,
`"builder"`, at the top of `Build.goal` and routes every per-step
line through it.

```
os/system/builder/
  Build.goal               // - set channel "builder" call BuilderChannel
                           //   ... foreach %goals%, call BuildGoal goal=%item%
  BuilderChannel.goal      // - write out %!data%
  EmitBuildEvent.goal      // - render template "/system/builder/templates/output/build-output.template"
                           //   - write out %msg% channel: "builder"
  templates/output/build-output.template   // Liquid case-block per event kind
```

Every call site that previously did `write out "..."` now does
`call /system/builder/EmitBuildEvent kind="...", <fields>`. The kind
discriminator is what selects a branch of the template (`build-path`,
`goalHeader`, `subGoalHeader`, `subGoalDone`, `goalError`, summary etc.).

The point of the indirection is the redirection seam. To replace the
plain `write out %!data%` sink with a file logger, a structured
JSON-Lines stream, or a TUI consumer, edit `BuilderChannel.goal` only —
no PLang source, no template, no other goal changes. The shape of each
event is fixed by `EmitBuildEvent`'s named parameters; the rendering
and the routing are decoupled.

`EmitSummary` (per-step `[≡]` cached / `[✓]` fresh marker + any
confidence warning) is always invoked — the historical gate
`if %!build.summary% is true` was dropped because the variable was
never propagated to sub-goal scope, suppressing all per-step output.

## Builder Version

Each built `.pr` file includes a `version` field set by the builder. This tracks which builder version produced the file.
