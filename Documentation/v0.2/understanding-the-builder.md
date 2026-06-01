# Understanding the PLang Builder

A conceptual guide for anyone learning how the PLang builder works and how the
runtime serves it. Read this first to get the mental model; then go to the
detailed references when you need specifics:

- [`build.md`](build.md) — the `plang build` CLI (`--build={...}`, cache, debug)
- [`build_process.md`](build_process.md) — the `.pr` file format and parsing
- [`building-the-builder.md`](building-the-builder.md) — rebuilding the builder
  itself (the bootstrap: cwd, file order, path-qualified filters)
- [`debug.md`](debug.md) — tracing LLM messages, variables, and resolve steps

---

## 1. What the builder is

PLang has **no parser**. A `.goal` file is natural language; the builder uses an
LLM to map each step to typed actions and writes the result as a `.pr` file
(JSON) that the runtime loads and executes directly.

```
.goal file (natural language) → builder (LLM) → .pr file (JSON) → runtime executes
```

The thing that surprises people: **the builder is itself written in PLang.** It
lives in `os/system/builder/*.goal`. So when you rebuild the builder, PLang is
building PLang — a bootstrap. The *running* builder executes from its own
compiled `.pr` files in `os/system/builder/**/.build/`, **not** from the `.goal`
source. This has a sharp consequence:

> Editing a builder `.goal` file does **not** change the current build run. Only
> the compiled `.pr` (or the `.llm`/template files, which are read fresh from
> disk every build) take effect immediately. To make a `.goal` change live, the
> builder must be rebuilt — and that rebuild runs on the *old* `.pr`.

See [`building-the-builder.md`](building-the-builder.md) for the bootstrap
mechanics and the cardinal rule (never hand-edit a `.pr` to mask a bad rebuild —
fix the prompt or validator instead; the one exception is a deliberate bootstrap
patch).

---

## 2. The two-phase pipeline: Plan → Compile

The builder splits the LLM work into two phases with different jobs.

### Plan (one call per goal)

The planner reads the whole goal and, **for each step**, names *which actions
are involved* — a set of `"module.action"` strings, unordered. It does **not**
fill parameters and does **not** decide chain order.

```json
{"index": 1, "actions": ["file.read", "cache.wrap", "variable.set"], "confidence": "VeryHigh"}
```

The planner's job is **recall, not precision**. Its prompt
(`os/system/builder/llm/Plan.llm`) explicitly says: when a step is ambiguous,
include *every* plausible candidate — extras are free, a wrong-only set
deadlocks. The compiler downstream picks the right one.

### Compile (one call per step)

The compiler takes one step plus the planner's action set for it, and produces
the final action chain: it decides **order**, **modifier nesting** (e.g.
`error.handle`, `cache.wrap`), **recovery placement**, and fills **parameter
values** from the step text. Its source of truth for chain shape is each
action's authored **Examples**.

```
formal: goal.call(GoalName={name:"Greet", parameters:[{name:"name", value:"world"}]})
```

The compiler writes a one-line **`formal`** notation first, then mirrors it into
`actions[]`. It's the syntax the LLM thinks in, and every `.pr` step carries it.
Full grammar, pattern catalog, and the known rough edges:
[`formal-plang.md`](formal-plang.md).

Why split them? The planner narrows a huge catalog down to a handful of
candidates cheaply; the compiler then reasons in depth about just those, with
their full schemas and examples loaded. It also lets the system prompt for the
compile call stay **stable across every step**, which makes provider-side prompt
caching effective — only the per-step user message varies.

---

## 3. The call chain (which file does what)

```
Build.goal                    Entry. Sets the "builder" output channel, loads the app
                              (builder.load), discovers goals (builder.goals), then
                              foreach goal → BuildGoal. Saves the trace manifest + app.
  BuildGoal.goal              Thin shim → BuildGoal/Start.
    BuildGoal/Start.goal      Per goal: run Plan, then foreach plan.steps → BuildStep/Start,
                              then recurse into sub-goals (BuildSubGoal). Saves trace,
                              calls builder.goalsSave to write the .pr.
      BuildGoal/Plan.goal     The Plan LLM call (QueryAndValidatePlan + Validate).
      BuildGoal/Validate.goal Structural validation of the plan (step count, shape).
      BuildGoal/LlmFixer.goal Re-prompts the planner when validation fails.
      BuildStep/Start.goal    Per step: Compile (validate+augment the action set, render
                              prompts, QueryAndVerify, builder.validate), then EmitSummary.
      BuildStep/Validate.goal Per-action validation (ValidateAction).
```

The LLM prompts live alongside, in `os/system/builder/llm/`:
`Plan.llm`, `Compile.llm`, `CompileUser.llm`, and the `templates/` that render
the goal text and per-step action details.

---

## 4. Plan phase in detail (`BuildGoal/Plan.goal`)

1. `builder.actions` → `%actions%` — the action catalog (see §6).
2. `render /system/actions/v2/summary.planner.md` → `%actionSummary%` — the
   catalog text injected into the planner **system prompt**.
3. `render goalFormat.template` → `%goalForLlm%` — the goal text as the **user
   message**.
4. `render Plan.llm` → the system prompt. `%messages%` = `[system, user]`.
5. `QueryAndValidatePlan` = `llm.query` (schema-constrained) → `%plan%`, then
   `Validate`. Wrapped in `on error call LlmFixer, then retry 2 times` so the
   retry surface catches **both** malformed JSON (the `llm.query` throws) **and**
   structural failures (`Validate` throws, e.g. step-count mismatch).

---

## 5. Compile phase in detail (`BuildStep/Start.goal`, the `Compile` goal)

1. `builder.validateStepActions` — validates the planner's set against the
   runtime catalog: **drops hallucinated entries** and **appends explicit
   `module.action` tokens** the planner missed.
2. `builder.actions` / `builder.types` → per-step `%actions%` / `%typeInfo%`,
   restricted to just the picked actions and the types they reference.
3. `goal.getTypes` → `%varTypes%` — the variable types in scope, recomputed per
   step so it reflects the actions written by **earlier** steps.
4. Render `stepForLlm`, `stepActionDetails` (each picked action's schema +
   Examples + **Notes**), `Compile.llm` (stable cross-cutting system prompt),
   and `CompileUser.llm` (the per-step user message).
5. `QueryAndVerify` = `llm.query` (the per-step output schema) → `%compileResult%`,
   plus a `missing-actions` check that throws if the set was too thin.
6. `builder.validate` against the compiled actions, with
   `on error call FixValidation, then retry 2 times`.
7. `EmitSummary` — emits the per-step `[✓] fresh` / `[≡] cached` line and any
   planner/compiler **Low/VeryLow** confidence warnings.

### Per-action prose lives in markdown, not C# attributes

The shape of an action (parameters, types, modifier role) is read from C#
attributes on the handler. Its **prose** — Description, Notes, Examples — lives
in `os/system/modules/<module>/{module,<action>}.{description,notes,examples}.md`.
The compiler's user message carries the Notes/Examples **only for the actions the
planner picked**. This is the main lever for fixing mapping quality: clarify the
description/examples, and the LLM stops guessing wrong.

---

## 6. The action catalog comes from the code

`builder.actions` calls `app.Module.Describe()` (`PLang/app/module/builder/code/Default.cs`).
`app.Module` is the **action registry** — `Describe()` enumerates every action
the runtime knows about, derived from the **source-generated action handlers**.

The practical upshot: **add a C# action and it shows up in the catalog
automatically.** You don't register it anywhere by hand. The builder's job
(`%actionSummary%` for the planner; per-action schema + Examples for the
compiler) is just rendering what `Describe()` returns.

---

## 7. Recovery loops

There are several distinct self-healing paths. Knowing which one fires tells you
where a build problem actually lives.

| Trigger | Handler | What it does |
|---|---|---|
| Planner returns malformed JSON / fails structural validation | `LlmFixer` (`BuildGoal/LlmFixer.goal`) | Re-prompts the **planner** with the error, `previousConversation=%plan%`. |
| Compiler says the action set is too thin (`missing-actions`) | `RefineActions` (`BuildStep/Start.goal`) | Re-asks the **planner** (continuation) for an expanded set, then retries the compile. |
| The validator rejects the compiled actions | `FixValidation` (`BuildStep/Start.goal`) | Re-prompts the **compiler** with the validator's complaint, `previousConversation=%compileResult%`, rewrites `goal.Steps`, retries `builder.validate`. |
| The compile `llm.query` itself errors (API/model error, unrecoverable) | `HandleStepFailure` (`BuildStep/Start.goal`) | Emits the `step-build-error` debug event, then rethrows. |

Retry counts (`then retry N times`) are the runtime's job, not a hand-rolled
counter — see [`builder-runtime.md`](builder-runtime.md) for `error.handle`
ordering semantics (`GoalFirst` vs `RetryFirst`).

---

## 8. The `.pr` file: one file, many goals

A `.pr` is one file per `.goal` **file**, and a file holds **many goals**:

- The **root** object is the **public** goal (`Start`, `Build`, …).
- Its **private** sub-goals are nested in `goals[]` (this is where
  `HandleStepFailure`, `QueryAndVerify`, etc. live).

Each step holds an `actions[]` array; each action is
`{module, action, parameters: [{name, value, type}], modifiers: []}`, plus a
human-readable `formal` line. Full format: [`build_process.md`](build_process.md).

---

## 9. Caching: there is only one cache

When `cache` is not set to `false`, the LLM layer (`OpenAi.cs`) hashes the LLM
message (plus a few properties) and looks it up in the local db
(`.db/system.sqlite`, `LlmCache` table). On a hit it returns the stored result
without calling the provider. That hit is what surfaces as `%compileResult.Cached%`
and the `[≡]` marker in build output; a fresh call shows `[✓]`.

`--build={"cache":false}` bypasses this lookup and forces a fresh call. **Always
use `cache:false` when validating a prompt or catalog change** — a stale cache
hit will hide whether your fix worked.

There is no second "kept mapping" cache layered on top — it's just this one LLM
cache.

---

## 10. Output and debug events

The builder writes all build output to a redirectable **"builder" channel**
(set up in `Build.goal`). Progress and errors are emitted by calling the
`EmitBuildEvent` goal, which renders a case in
`os/system/builder/templates/output/build-output.template` and writes it to that
channel. Event kinds include `build-path`, `goals-found`, `goalHeader`,
`step-fresh` / `step-cached`, `confidence-warning-planner` /
`confidence-warning-compiler`, and `step-build-error`. Swap the channel's backing
goal to redirect all build output somewhere else.

To see exactly what the LLM received and returned, use `--debug` rather than
adding diagnostics — e.g.
`plang build '--build={"cache":false}' '--debug={"llm":{"system":true,"response":true},"maxLength":50000}'`.
Full options in [`debug.md`](debug.md).

---

## 11. The default model

The builder's LLM calls use the default model `gpt-5.4-nano` (`OpenAi.cs`) unless
a step pins one explicitly with `model="..."` on the `llm.query`. Pinning a
heavier model is a blunt instrument — it can trade one failure mode for another
(e.g. fewer mis-selections but more malformed JSON). Prefer fixing mapping
quality at the prompt/catalog layer (§5, §6) over changing the model.

---

## 12. Debugging a build — traces and `--debug`

Two complementary tools. **Traces** are written automatically and tell you *what
the builder produced and what the LLM saw*. **`--debug`** is opt-in and tells you
*how a value flowed through execution*.

### Trace files (always written, no flag needed)

Every build invocation writes a trace, regardless of `--debug`:

```
.build/traces/{trace.id}/
├── manifest.json     ← the goal names built in this run
├── Start.json        ← per-goal build trace
├── BuildGoal.json
└── ...
```

`trace.id` is `{ticks}_{guid8}` — sortable by start time, so a directory listing
shows runs in order, and each run gets its own folder (nothing overwrites). Each
per-goal `<Goal>.json` holds:

| Field | What it captures |
|---|---|
| `goal` | the parsed goal (steps, paths, hash) |
| `plan` | the planner pass: `description`, `steps` (the action sets it chose), `system` (the **exact** planner system prompt = rendered `Plan.llm` + `%actionSummary%`), `user` (the goal text sent), `usage` (model, token counts, cost, cached) |
| `stepPasses[]` | one entry per compiled step: `stepText`, `actions`, `user` (the `CompileUser` message), `response` (the compile result), `usage` |
| `compileSystem` | the compile system prompt (rendered once per goal) |
| `durationMs`, `subGoals[]` | timing and the nested sub-goal traces |

What traces are good for:
- **Seeing exactly what the LLM received** — the rendered `system`/`user` prompts
  for both phases, without re-running anything.
- **Auditing what the builder produced** for a goal, and comparing two runs
  side by side (separate folders).
- **Token/cost accounting** per phase via `usage`.

> **Caveat:** the `response` in `stepPasses` is captured *after*
> `builder.validate`/enrich runs, so parameter values may already be normalized
> (e.g. a raw `"data.txt"` turned into a `Path` object). Do **not** read traces to
> learn what the LLM *literally* returned — for the raw API payload use
> `--debug={"llm":{"response":true}}`.

Clean up a single run without touching others: `rm -rf .build/traces/{trace.id}/`.

### `--debug` — following a variable through the system

All `--debug` output goes to **stderr** (it never pollutes program output), and
the JSON maps onto `Debug.@this` (`PLang/app/module/debug/this.cs`). The pieces
that answer "where did this variable come from / change / go wrong?":

- **Watch a value at step boundaries** — see it BEFORE/AFTER each step:
  ```bash
  plang build '--build={"files":"x.goal","cache":false}' '--debug={"variables":[{"name":"actionSummary"}]}'
  ```
- **Track every mutation** — log each time the variable is replaced, with the
  goal, step, old/new CLR type, and a C# stack trace (the top frames) showing
  *who* changed it. This is the tool for "this variable became the wrong thing —
  where?":
  ```bash
  plang '--debug={"variables":[{"name":"trace","event":"onchange"}]}'
  ```
  Events: `oncreate`, `onchange`, `ondelete`, `ontypechange` (fires only when the
  value's CLR type changes — e.g. Dictionary → String).
- **Drop to action granularity** — `level:"action"` shows BEFORE/AFTER for each
  action *within* a step, so you can watch `%!data%` flow between chained actions
  (e.g. `goal.call` → `variable.set`):
  ```bash
  plang '--debug={"level":"action","variables":["%!data%"]}'
  ```
- **Trace resolution** — `resolveTrace:true` logs every `%variable%` resolution
  with the resolved type and nesting depth. Use it when a `%var%` resolves to
  null or the wrong member and you need to see the lookup path.
- **Scope and trim the noise** — `goal` / `step` filter to one place, `grep` is a
  case-insensitive regex over output lines, `maxLength` caps line length:
  ```bash
  plang build '--build={"cache":false}' '--debug={"goal":"BuildGoal","step":3,"grep":"Module","maxLength":2000}'
  ```

The combination — read the trace to see *what* the builder produced and what the
LLM saw, then re-run with `--debug` variable watches to see *how* a value got
there — is the standard loop for diagnosing a bad mapping without adding a single
line of diagnostic code. (Diagnostics in C# go through `context.App.Debug.Write`,
gated on `--debug`; never `Console.WriteLine`.) Full options:
[`debug.md`](debug.md).

---

## Mental model in one paragraph

The builder is PLang building PLang. It runs **two LLM phases** — a cheap
**Plan** pass that recalls the candidate `module.action`s per step, then a deep
**Compile** pass that orders them, nests modifiers, and fills parameters from the
step text using each action's authored Examples. The candidate catalog is
generated from the C# action handlers via `app.Module.Describe()`, so new actions
appear automatically. Everything flows through **one LLM cache** at the OpenAi
layer (`cache:false` to bypass). The output is a `.pr` per file holding the
public goal plus its private sub-goals. Because the running builder executes from
its own `.pr`, changing the builder is a bootstrap: edit the `.goal`/prompt,
rebuild on the old `.pr`, and let the recovery loops (`LlmFixer`, `RefineActions`,
`FixValidation`, `HandleStepFailure`) absorb the rough edges.
