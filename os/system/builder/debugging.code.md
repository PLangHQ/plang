# Debugging Builder Failures — a Runbook

Read this when a build produces a **wrong `.pr`** — wrong module/action, a dropped action,
an invented parameter — especially when it's *deterministic* and *context-dependent* (some
goals fine, others broken). For the pipeline this debugs, read
[`builder.code.md`](builder.code.md) first.

A wrong mapping has exactly **three** possible origins. The whole point of triage is to find
which one **before** touching anything:

```
   goal text
      │
      ▼
 ┌──────────┐  Pick (which actions each    ┌──────────┐  formal (order + values,   .pr
 │ DECIDER  │ ───────── step may use) ────▶ │  WRITER  │ ──── whole goal) ────────▶ code[]
 └──────────┘  step.Pick                    └──────────┘  one llm.query
      ▲                                          ▲
 decider templates + decider.json          properties.template (the RENDERED user message)
 + action descriptions/examples            + Properties.llm
```

1. **Decider** listed the wrong actions for a step → fix its inputs: `llm/decider.json`
   (common/popular), the decider templates, or the action **descriptions/examples** it reads.
2. **Writer** got the right picks but wrote wrong formal → fix `llm/Properties.llm` or the
   per-action **notes/examples** markdown.
3. **The rendered prompt is wrong/empty** → the model was never *told* the right thing. This
   is a **plumbing bug** (a template or a value-binding bug), not a prompt bug — fixing
   teaching does nothing. Check it first; most "the LLM is dumb" reports are this.

## The single most useful artifact: the writer's user message

The Properties user message (`llm/templates/properties.template`) shows, per step, exactly
what the decider decided and what the writer started from:

```
  [3]  - read 'notes.txt', write to %content% => decider: file.read 0.99, variable.set 0.85 (write to) => formal: file.read(Path=?); variable.set(Name=%content%, Value=%!data%)
```

- **`=> decider:`** — the actions the decider listed for this step, with scores and marks
  (`(possible)`, `(write to)`, `(possible, popular)`). Empty / "(nothing it is sure of)"
  means the decider gave this step nothing.
- **`=> formal:`** — the writer's starting line (the certain actions, `?` for values still
  needed). Absent when nothing is certain.

Read this one block and the origin is usually obvious:

| What you see | Origin | Fix surface |
|---|---|---|
| `=> decider:` is missing the right action (or empty) | **#1 decider** | `decider.json`, decider templates, the action's `description.md` / `examples.md` |
| `=> decider:` lists the right actions, but the written formal (the `.pr` `code`) is wrong | **#2 writer** | `Properties.llm`, the action's `notes.md` / `examples.md` |
| the whole block is malformed, or a `%var%` is unresolved, or a listed action's signature/notes are missing from the Types/actions section | **#3 plumbing** | the template + its value binding (Fluid) |

## Triage commands

Work in `Tests/`. Pick a deterministic repro goal. A `cache:false` rebuild **overwrites the
committed `.pr`** — copy it aside first and restore after, so a diagnostic run never lands a
bad `.pr` in git:

```bash
cp App/Some/.build/some.pr /tmp/good.pr
BIN=../PlangConsole/bin/Debug/net10.0/plang
```

### What did the DECIDER see and answer?

The decider runs in `BuildGoal/Decide.goal` (the `Decide` goal). Watch its rendered State,
its questions, and its answers:

```bash
$BIN build '--build={"files":["<goal>"],"cache":false}' \
  '--debug={"goal":"Decide","variables":["state","questions","answer"]}' > /tmp/decide.txt 2>&1
cp /tmp/good.pr <restore the .pr>
```

- `%state%` — the decider's State (how PLang is structured + the goal's steps + the modules
  and their actions). Stage 1 lists every module; stage 2 only the picked ones.
- `%questions%` — the questions JSON (`decider1.template` / `decider2.template`).
- `%answer%` — the decider's answers, keyed by question id (`s<i>_@module`, `s<i>_<mod.act>`).

If the State doesn't describe an action, or describes it confusingly, the decider can't pick
it — that's a **decider-input** fix (the action's `description.md`, or `decider.json` if it's
a common/popular action), not the writer.

### What was the WRITER told, and what did it write?

The writer runs in `BuildGoal/Properties.goal` (the `Properties` goal), one `llm.query`:

```bash
$BIN build '--build={"files":["<goal>"],"cache":false}' \
  '--debug={"goal":"Properties","variables":["propertiesUserMsg"],"llm":{"system":true,"response":true},"length":{"max":50000}}' > /tmp/props.txt 2>&1
cp /tmp/good.pr <restore the .pr>
```

- `%propertiesUserMsg%` (and the `LLM USER` block) — the rendered user message: the goal with
  each step's `=> decider:` / `=> formal:` / `=> types:` lines, then the Types / Settings /
  Keys blocks, then each listed action's signature + description + notes. **This is the
  artifact from the table above.**
- `LLM RESPONSE` — the raw formal the writer returned (`[i] module.action(Name=value); …`),
  before `build.match` reads it in.

> The `--debug` `llm` object emits only the flags you set, each as its own
> `=== LLM SYSTEM/USER/RESPONSE/SCHEMA ===` block on the debug channel (stderr, truncated to
> `length.max`); redirect to a file for the full text. This is the raw API payload — unlike
> the trace, which records only `{id, timestamp, goal, subGoals, durationMs}` and, on failure,
> a `buildError`. **Use `--debug` for what the LLM literally saw/returned; use the trace only
> for which goals ran and timing.**

## Origin #3 — the rendered prompt is empty or wrong

**Symptom:** the `=> decider:` block is empty or a listed action's detail is missing from the
user message even though the decider *did* answer — i.e. the data exists but the template
rendered nothing. The writer then guesses from step text alone: easy verbs land right and the
goal "looks fine"; ambiguous verbs land wrong, sometimes with invented parameters. That's why
the failure looks like LLM non-determinism — it's a blind guess landing differently per verb.

**Root cause is template/binding, not prompt.** The prompts are Liquid templates rendered
through the **Fluid** engine (`PLang/app/module/action/ui/code/Fluid.cs`). The classic version
of this bug: a value read from a file (`read … write to %x%`) reaches Fluid as a lazy `file`
reference whose content is a `clr(JsonElement)`, and an old Fluid converter couldn't iterate
it — so `{% for c in decider.common %}` rendered nothing and **stage 1 never asked the common
questions**. (Fixed in `Fluid.cs` by routing any PLang container through one value door; see
[`BuildGoal/Decide.code.md`](BuildGoal/Decide.code.md) "Known issues".) The lesson stands:

> **Quick confirm a value won't render:** if `%x%` resolves under `--debug` but `{{ x.field }}`
> / `{% for i in x %}` renders empty in the template, `x`'s CLR shape isn't Fluid-iterable.
> The fix is in `Fluid.cs` (a C# change — goes to coder under "treat C# as fixed"), **not** in
> the teaching prompts.

Related born-typed footgun worth knowing: `set %x% = …, type=json` is invalid on the
born-typed model (`json` is a serialization format, not a type). A value set that way can
resolve to a **typed-null** at a typed consumer, surfacing as a misleading
`%X% holds a <type> — 'list' cannot be created from it` even though `--debug` shows a real
value at the step boundary. Fix: drop `type=json` so the literal borns as a native `list`/`dict`.

## Trace & binary gotchas

- The trace (`.build/traces/{id}/`) does **not** carry the LLM payloads any more — it's goal
  names + timing + any `buildError`. For payloads use `--debug` (above).
- A `cache:false` rebuild overwrites the committed `.pr` — `cp` it aside and restore.
- **Stale-binary trap:** `plang build`/`--test` use the pre-built
  `PlangConsole/bin/Debug/net10.0/plang`. After a C# change, `dotnet build PlangConsole` before
  trusting any result. See the repo `CLAUDE.md`.

## See also

- [`builder.code.md`](builder.code.md) §9–§10 — output events and traces.
- [`BuildGoal/Decide.code.md`](BuildGoal/Decide.code.md) — the decide stage and its known issues.
- [`../modules/catalog.md`](../modules/catalog.md) — editing the action teaching (#1 and #2 fixes).
- [`../../../Documentation/v0.2/debug.md`](../../../Documentation/v0.2/debug.md) — the full `--debug` property bag.
