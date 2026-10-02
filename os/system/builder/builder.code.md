# Understanding the PLang Builder

The conceptual guide to how the builder works. Read this first for the mental
model; then go to the companions for specifics:

- [`start.md`](start.md) — the index to all builder docs (what each one is for)
- [`bootstrap.code.md`](bootstrap.code.md) — rebuilding the builder itself (the bootstrap)
- [`debugging.code.md`](debugging.code.md) — triaging a wrong `.pr`
- [`pr-format.code.md`](pr-format.code.md) — the `.pr` file format the builder produces
- [`BuildGoal/Decide.code.md`](BuildGoal/Decide.code.md) — the decide stage in depth
- [`../modules/catalog.md`](../modules/catalog.md) — the action catalog & teaching the builder sends the LLM
- [`../../../Documentation/v0.2/build.md`](../../../Documentation/v0.2/build.md) — the `plang build` CLI

---

## 1. What the builder is

PLang has **no parser**. A `.goal` file is natural language, written in any human
language. The builder turns each step into typed actions and writes the result as
a `.pr` file (JSON) that the runtime loads and executes directly.

```
.goal file (natural language) → builder → .pr file (JSON) → runtime executes
```

The thing that surprises people: **the builder is itself written in PLang.** It
lives in `os/system/builder/*.goal`. So when you rebuild the builder, PLang is
building PLang — a bootstrap. The *running* builder executes from its own compiled
`.pr` files in `os/system/builder/**/.build/`, **not** from the `.goal` source. One
sharp consequence:

> Editing a builder `.goal` file does **not** change the current build run. Only
> the compiled `.pr` — or the `.llm`/`.template`/`.json` files, which are read fresh
> from disk every build — take effect immediately. To make a `.goal` change live,
> the builder must be rebuilt, and that rebuild runs on the *old* `.pr`.

See [`bootstrap.code.md`](bootstrap.code.md) for the bootstrap mechanics and the
cardinal rule (never hand-edit a `.pr` to mask a bad rebuild — fix the prompt,
template, or the C# that feeds them).

---

## 2. The pipeline: Decide → Properties → match

The builder compiles **one goal at a time** (the top goal, then each sub-goal). For
one goal, the work is three stages, all in `BuildGoal/Start.goal`'s `Compile` goal:

```
Decide      which actions each step MAY use      (two decider exchanges, whole goal)
Properties  WHICH of them, in what order, with    (one llm.query, whole goal)
            what values — written in "formal"
match       read the formal lines into each       (no LLM — build.match parses)
            step's code
```

Two different kinds of LLM call do the two thinking stages, and that split is the
heart of the design.

### Decide — the decider (closed-set questions)

A **generic** model (`llm.decider`) answers *typed questions*: a `choice` picks one
of a closed set, a `noul` ("no/unless likely") answers yes/no with a probability. It
is handed a **State** (shared context, given once) and a dict of **Questions**, and
returns one answer per question id. It knows nothing about goals or steps — everything
goal-shaped is composed in PLang (the templates write the words, `build.pick` reads
the numbers). Two exchanges:

- **Stage 1** — per step: one `choice` ("which module does the main work?") plus one
  yes/no per **common action** (`llm/decider.json`'s `common`: `variable.set`,
  `goal.call`, `loop.foreach`, `goal.return`, `output.write`, `on.error`, `on.cache`,
  `on.timeout`, `condition.if`, `file.read`).
- **Stage 2** — only what stage 1 left open, as each step's `Pick` decides: the main
  module's action, a runner-up module, whether the step also uses a module it scored
  under certain, an if's branches, and — on a step stage 1 was unsure of — which
  **popular action** (`decider.json`'s `popular`) it uses.

`build.pick` reads both answers onto each step as its **`Pick`**
(`step.Pick`: `.Module`, `.Question`, `.Listed`, `.Formal`, `.IsCondition`,
`.IsUnsure`). Full detail: [`BuildGoal/Decide.code.md`](BuildGoal/Decide.code.md).

### Properties — the writer (open-ended formal)

This one *is* a plain `llm.query`, not a decider question: order and parameter values
are **written**, not picked from a closed set. **One request for the whole goal**,
because steps refer to each other — `%!data%` means "the action before this one", and
a model shown one step alone can't see that.

The answer is text, **one line per step in formal** notation:

```
[0] file.read(Path="notes.txt"); variable.set(Name=%content%, Value=%!data%)
[1] output.write(Data=%content%)
```

Each step's line starts from a `=> formal:` **seed** that the decider's picks produced
(the certain actions, with `?` where a value is still needed). The LLM fills the `?`s,
adds the properties the step's words give, and orders the actions. System prompt =
`llm/Properties.llm`; user message = `templates/properties.template`. Formal grammar
is taught inside `Properties.llm` itself (quoted text, `%var%`, `%!data%`, lists,
dicts, a condition's `{ }` body, clauses like `on.error(...)`).

### match — read it in (no LLM)

`build.match` parses the formal lines into each step's `code`. No model call. It is
where structural rules are enforced and recoverable errors are raised (§4).

### Why this shape

The decider narrows a large catalog to a handful of candidates with cheap, closed-set
questions whose answers can't hallucinate a parameter. The writer then reasons about
just those candidates, once, with the whole goal visible so cross-step `%!data%` wiring
is correct. The decider's State and the writer's system prompt stay **stable across every
step of a goal**, which keeps provider-side prompt caching effective — only the
per-question / per-step content varies.

---

## 3. The call chain (which file does what)

```
Build.goal                 Entry. Sets the "builder" output channel (BuilderChannel),
                           build.load (app identity), build.goals (discover .goal files),
                           foreach goal → BuildGoal. Then build.unreached, save the trace
                           manifest, build.appSave.
  BuildGoal.goal           Thin shim → BuildGoal/Start.
    BuildGoal/Start.goal   Per goal: the Compile goal (Decide → Properties → build.match),
                           then foreach sub-goal → BuildSubGoal (same three stages), then
                           build.fold and build.goalsSave (write the .pr). Owns the recovery
                           goals FixSteps, ConfirmNumbers, SourceError, HandleBuildFailure.
      BuildGoal/Decide.goal     Stage 1+2 of the decider (renders the templates, llm.decider,
                                build.pick). See Decide.code.md.
      BuildGoal/Properties.goal The Properties llm.query (renders properties.template +
                                Properties.llm).
  BuilderChannel.goal      The redirectable sink the "builder" channel is bound to.
  EmitBuildEvent.goal      One-call build-time output; renders a case in
                           templates/output/build-output.template to the "builder" channel.
```

The prompts and their data live in `os/system/builder/llm/`:

| File | Role |
|---|---|
| `Properties.llm` | the writer's **system prompt** (formal grammar + rules) |
| `SourceFix.llm` | system prompt for `SourceError` (rewrite the programmer's bad PLang) |
| `decider.json` | the **common** actions (stage 1 yes/no) and **popular** actions (stage 2) |
| `templates/decider.state.template` | the decider's State (how PLang is structured + the goal's steps + the modules) |
| `templates/decider1.template` / `decider2.template` | stage 1 / stage 2 questions |
| `templates/properties.template` | the writer's user message (goal, each step's `=> decider:`/`=> formal:`/`=> types:`, the Types / Settings / Keys blocks, then each listed action once) |
| `templates/confirm.state.template` / `confirm.template` | the `ConfirmNumbers` decider exchange |

---

## 4. Recovery loops

`build.match` raises recoverable errors; `BuildGoal/Start.goal`'s `Compile` step wires a
recovery goal to each. Knowing which fires tells you where a build problem lives.

| Error key | Recovery goal | What it does |
|---|---|---|
| `UnwrittenNumber` | `ConfirmNumbers` | The writer wrote a digit the step's words don't ("retry once" → `RetryCount=1`). The decider is asked, one yes/no per number, whether the words give it (in any language). `build.match` runs again with those answers; a denied number is refused as invented. |
| `ElseWithoutIf` | `SourceError` | The **programmer's** PLang is wrong (an `else` in a step apart from its `if`), not the LLM's answer. `SourceFix.llm` writes the corrected PLang lines; the same error is rethrown carrying them, and this goal's build stops. Nothing is retried. |
| (any other) | `FixSteps` | Some steps were refused. The writer is re-prompted with each refusal, **continuing the same conversation**, and answers again **only** the refused steps; the others keep the code they took. |

A goal-level failure (anywhere in `Compile` or `build.fold`) is caught by
`HandleBuildFailure`, which records the error on the trace, saves the trace, and rethrows.

Retry counts on the recovery goals are the runtime's job (`on error … then retry N times`),
not a hand-rolled counter.

---

## 5. The action catalog comes from the code

The decider's State and the writer's user message list the modules and actions from
`%!app.module.list%` — i.e. `app.Module` (the action registry), whose entries are derived
from the **source-generated action handlers**. Add a C# action and it appears in the
catalog automatically; you register nothing by hand.

Each action's **shape** (parameters, types, defaults, modifier role) comes from the C#
attributes. Its **prose** (Description, Notes, Examples) comes from markdown under
`os/system/modules/<module>/`. The prose is read fresh from disk every build — tuning
builder quality ships without a C# rebuild. **The markdown is your main quality lever.**
Full guide: [`../modules/catalog.md`](../modules/catalog.md).

---

## 6. The `.pr` file: one file, many goals

A `.pr` is one file per `.goal` **file**, holding **many goals**: the root (public) goal
is the JSON root, its private sub-goals nest in `child[]`. Each step holds `code[]` (its
actions); a condition's body nests as `code[].child[]`. There is no `formal` field and no
`modifiers` array in the `.pr` — the formal notation is a build-time wire, and clauses are
their own `code` entries. Full format: [`pr-format.code.md`](pr-format.code.md).

A step that is unchanged since its last build is **cached**: its saved `code` stands and
the decider/writer never see it (`step.IsCached`). A step already written in formal
(`step.IsFormal`) reads its own text and needs no decider answer. When the catalog changes
under a built step (a parameter gone, a slot's kind changed), the step **reopens** and is
rebuilt — you'll see a `Reopened` warning in the `.pr`.

---

## 7. Caching: there is only one cache

When `cache` is not `false`, the LLM layer hashes each request (messages + model +
temperature + schema + format) and looks it up in `.data/data.sqlite` (`LlmCache`). A hit
returns the stored result without calling the provider; that's the `[≡]` marker in build
output, versus `[✓]` for a fresh call. `llm.decider` is **not** cached.

`--build={"cache":"skip"}` bypasses the lookup and forces fresh calls. **Always use
`cache:skip` when validating a prompt, template, or catalog change** — a stale hit hides
whether your fix worked. There is no second "kept mapping" cache on top; the step-level
"cached" in §6 is a *source-unchanged* skip, decided before any LLM call.

---

## 8. The default model

The builder's `llm.query` / `llm.decider` calls pin `gpt-5.4-nano` explicitly in the goals.
The decider goes through `IDecider` → `TypeSafe` (`PLang/app/module/llm/code/TypeSafe.cs`),
which posts to the "systemone" endpoint. Pinning a heavier model is a blunt instrument —
prefer fixing mapping quality at the prompt / catalog layer (§5) over changing the model.

---

## 9. Output and debug events

All build output goes through a redirectable **"builder" channel**, set up in `Build.goal`
and backed by `BuilderChannel.goal`. Progress and errors are emitted by calling
`EmitBuildEvent kind="…"`, which renders a case in
`templates/output/build-output.template`. Event kinds include `build-path`, `goals-found`,
`goalHeader`, `subGoalHeader`/`subGoalDone`, `goal-decided`, `goal-properties`,
`properties-rejected`, `goalError`, `step-cached`/`step-fresh`. Swap the channel's backing
goal to redirect all build output.

To see exactly what the LLM received and returned, use `--debug` rather than adding
diagnostics:

```bash
plang build '--build={"cache":"skip"}' '--debug={"llm":{"system":true,"response":true},"length":{"max":50000}}'
```

Full options: [`../../../Documentation/v0.2/debug.md`](../../../Documentation/v0.2/debug.md).

---

## 10. Traces

Every build writes a trace under `.build/traces/{trace.id}/`, regardless of `--debug`.
`trace.id` is `{ticks}_{guid8}` — sortable by start time, each run its own folder:

```
.build/traces/{trace.id}/
├── manifest.json     ← the goal names built in this run (%traceGoals%)
├── Start.json        ← per-goal trace: {id, timestamp, goal, subGoals[], durationMs}
└── …                 ← one <Goal>.json per goal built
```

A per-goal trace carries the goal name, its sub-goals, timing, and — on failure — a
`buildError` block (`key`, `message`, `goal`). Traces are good for seeing which goals a
run built, comparing two runs side by side (separate folders), and timing. For what the
LLM *literally* received or returned, use `--debug` (above), not the trace.

Clean up one run without touching others: `rm -rf .build/traces/{trace.id}/`.

---

## Mental model in one paragraph

The builder is PLang building PLang. For one goal it runs a **decider** (closed-set,
can't-hallucinate questions that name *which actions each step may use*), then **one
writer** `llm.query` over the whole goal that writes each step's actions in **formal**
(order + parameter values, with cross-step `%!data%` wiring visible), then `build.match`
reads the formal in with no LLM, and `build.fold` nests indented sub-steps under their
condition. The candidate catalog is generated from the C# action handlers, so new actions
appear automatically, and their markdown teaching is the main quality lever. Everything
flows through one LLM cache (`cache:skip` to bypass). The output is a `.pr` per file
holding the public goal plus its private sub-goals. Because the running builder executes
from its own `.pr`, changing it is a bootstrap: edit the `.goal`/prompt, rebuild on the
old `.pr`, and let the recovery goals (`FixSteps`, `ConfirmNumbers`, `SourceError`) absorb
the rough edges.
