# Debugging Builder Failures — a Runbook

Read this when a build produces a **wrong `.pr`** (incorrect module/action mapping,
hallucinated actions, dropped modifiers, invented parameters) — especially when it's
*deterministic* and *context-dependent* (some goals fine, others broken on rebuild).

The builder is a two-phase LLM pipeline. A wrong mapping has exactly **three**
possible origins, and the whole point of debugging is to find which one **before**
touching anything:

```
   goal text
      │
      ▼
 ┌─────────┐   plan (action sets per step)    ┌──────────────┐   compile      .pr
 │ PLANNER │ ──────────────────────────────▶  │   COMPILER   │ ────────────▶  actions[]
 └─────────┘   %plan.steps[i].actions%         └──────────────┘
      ▲                                              ▲
  Plan.llm                                  CompileUser.llm  ← the RENDERED user message
  (system prompt)                           (the actions + schemas the compiler is told about)
```

1. **Planner** chose the wrong action set → fix `Plan.llm` or planner teaching.
2. **Compiler** got the right set but emitted wrong `actions[]` → fix `Compile.llm`
   or the per-action `notes`/`examples` markdown.
3. **The rendered user message is wrong/empty** → the compiler was never *told* the
   right thing. **This is a plumbing bug, not a prompt bug** — fixing teaching will
   do nothing. Look at the templates and the C# template binding.

Most "the LLM is dumb" reports are actually #3. Check it first.

---

## The 3-minute triage

Working dir `Tests/`. Pick a deterministic repro goal. **Save the good `.pr` first**
(`cache:false` rebuild overwrites it):

```bash
cp App/CallStack/.build/handledflagsetwhenrecoverysucceeds.test.pr /tmp/good.pr
BIN=../PlangConsole/bin/Debug/net10.0/plang
```

### Step A — is the PLANNER right?

```bash
$BIN build --build='{"files":["<goal>"],"cache":false}' \
  '--debug={"goal":"QueryAndValidatePlan","llm":{"system":true,"user":true,"schema":true,"response":true}}' > /tmp/plan.txt 2>&1
cp /tmp/good.pr <restore the .pr>
```

> **`llmTrace` is gone** — replaced by the granular **`llm`** object. Only the flags
> you set to `true` are emitted, each as its own `=== LLM SYSTEM/USER/SCHEMA/RESPONSE ===`
> block: `system` (system prompt), `user` (user message — the single most useful one),
> `schema` (the schema string sent), `response` (the raw model reply). Add
> `"output":"file"` to write each block to a file instead of stderr. All-off / no `llm`
> object means no tracing.

The `llm.query` lives in the `QueryAndValidatePlan` **sub-goal**, not in `Plan` —
trace the sub-goal or you'll only see the orchestration. The `LLM USER` block is the
literal user message **as sent** — if it shows an unresolved `%var%` (e.g. `%goalForLlm%`
instead of the rendered goal), the bug is message-content resolution, not the model.
The `LLM RESPONSE` block is the raw planner JSON:

```
%plan% = {description:…, steps:[{index:0, actions:[error.throw, error.handle, variable.set]},
                                {index:1, actions:[assert.isTrue]},
                                {index:2, actions:[assert.isNull]}]}
```

- **Planner set is wrong** → origin #1. Stop here; fix `Plan.llm` / planner teaching.
- **Planner set is right** (as above) → the bug is downstream. Continue.

### Step B — is the COMPILER right, and WHAT WAS IT TOLD?

```bash
$BIN build --build='{"files":["<goal>"],"cache":false}' \
  '--debug={"goal":"Compile","llm":{"user":true,"schema":true,"response":true}}' > /tmp/compile.txt 2>&1
cp /tmp/good.pr <restore the .pr>
```

**Read `%buildStepUserMsg%` — the actual user message the compiler received.** This
is the single most important artifact and the step people skip. Dump it in full
(raise `maxLength`); look at these sections:

```
## What to map to — the planner picked these actions
error.throw, error.handle, variable.set          ← MUST list the planner's set

## Action detail (parameters + notes)
## error.throw … Parameters: … Notes: … Examples: …   ← MUST show each picked action's schema

## Variables in scope (from prior steps)
`%recovered%` (bool), …                          ← MUST list known vars
```

- **These sections are EMPTY** → origin #3, the **blind-compiler failure class**
  (next section). Do NOT touch teaching prompts — the compiler can't see them.
- **Sections are populated but the compile output is still wrong** → origin #2; the
  compiler genuinely mis-mapped. Fix `Compile.llm` or the per-action markdown.

The compiler's own `errors[]`/`explanation` in its response is a reliable tell: an
`insufficientContext` / "the planner-provided action set was not included" message
is the compiler telling you, truthfully, that section B was empty.

---

## The blind-compiler failure class (origin #3)

**Symptom:** `%buildStepUserMsg%` has empty "What to map to" and "Action detail"
sections even though the planner returned a correct set. The compiler, given no
candidates and no schemas, guesses from step text alone:

- Easy/unambiguous verbs (`set`, `add`, `equals`, `sort`) → guessed right → goal
  builds → *looks fine*.
- Ambiguous or domain-loaded verbs (`assert … is null`, anything near `%!error%`)
  → guessed wrong, often `error.throw`, sometimes with **invented parameters**
  (`error.throw(Condition=…)` — no such param) → goal fails.

This is why the failure is **context-dependent and looks like LLM
non-determinism**: it's the same blind guess landing differently per verb. It also
means a goal can pass with a *coincidentally correct* `.pr` while the teaching layer
is doing nothing.

**Root cause is template/binding, not prompt.** The compile user message is built by
Liquid templates rendered through the **Fluid** engine
(`PLang/app/module/ui/code/Fluid.cs`):

- `os/system/builder/llm/CompileUser.llm` — the user-message template.
- `os/system/builder/llm/templates/stepActionDetails.template` — the per-action
  schema/notes block (`{{ actionDetails }}`).

> **⚠️ `type=json` is deprecated — `json` is NOT a type (it's a serialization
> format).** A `{…}` literal is a `dict`, a `[…]` literal is a `list`, nested →
> `list<dict<string,data>>`. The `set %x% = …, type=json` form below describes the
> *historical* behavior; on the born-typed model `type=json` is invalid and is being
> removed from the builder goals + teaching. It bites **two** ways:
> 1. **(historical / Fluid)** it stored a `JsonNode`, not Fluid-readable — the
>    origin-#3 class documented here.
> 2. **(born-typed)** a value set with `type=json` resolves to a **typed-null
>    (`app.type.null.@this`)** at a *typed* consumer. E.g. `llm.query Messages` is
>    `list<llmmessage>`; reading `%messages%` (set via `type=json`) through the typed
>    door (`Value<list<llmmessage>>()`) yields a `null.@this` stamped with the *asked*
>    type, so it declines:
>    `%Messages% holds a list<llmmessage> — 'list' cannot be created from it` — even
>    though `--debug` shows `%messages%` as a real list at the step boundary. The tell
>    is the misleading "holds a `<asked-type>`" message: the value is value-less, the
>    type in the message is the *slot's* declared type, not the value's. Fix: drop
>    `type=json` so the literal borns as a native `list`/`dict`. The conversion path
>    itself (`list<dict>`/`JsonNode`/json-string → `list<llmmessage>`) is *not* the
>    gap — it converts fine in isolation; the value being null is the bug.

Both iterate `{% for a in planStep.actions %}`. **`planStep` is built with
`set %plan% = … type=json`, which TypeMapping stores as a
`System.Text.Json.Nodes.JsonNode`** (`PLang/app/module/variable/set.cs`). Native
PLang `dict`/`list` values (`app.type.{dict,list}.@this`) and `JsonNode`:

- ✅ are readable by **PLang's own** dot-path resolver (`%planStep.actions%` works —
  see the `JsonObject` arm in `app/variable/navigator/Dictionary.cs`),
- ❌ are **NOT** readable by Fluid. They implement domain interfaces
  (`IEquatableValue`, `IListLeaf`, `IBooleanResolvable`) but **not** `IEnumerable`
  or `IDictionary`, and `JsonObject` reflects to `Count/Options/Parent/Root` — never
  its keys. So `planStep.actions` is `nil` in a template → the loop renders nothing.

Only **real .NET `List<T>`/`Dictionary`/POCOs** render in Fluid. (That's why the
action *catalog* `%actions%`, a real `List<…>`, renders fine while the plan doesn't.)

**There is no PLang-layer workaround** — coercing to a native collection still isn't
Fluid-readable. The fix is in `Fluid.cs`: materialize PLang native collections /
`JsonNode` to plain CLR `Dictionary`/`List` **before** `FluidValue.Create` in the
variable-binding loop. (C# change → goes to coder under the "treat C# as fixed" rule.)

**Quick confirm a value won't render in a template:** if `%x%` resolves in PLang
debug but `{{ x.field }}` / `{% for i in x %}` renders empty, `x`'s CLR type is a
native collection or `JsonNode`, not a plain CLR collection.

---

## Trace mechanics worth knowing

- The `llm.query` for each phase is inside a **sub-goal** (`QueryAndValidatePlan`
  for plan, `QueryAndVerify` for compile). Trace the sub-goal, not the parent.
- The trace's `stepPasses[].response` is captured **after** `builder.validate`/enrich
  — it's normalized, not the literal API return. For the raw payload use
  `--debug={"llm":{"response":true}}`, or read the `RawResponse` property off the
  result Data (`%compileResult!RawResponse%` / `%plan!RawResponse%`).
- A `cache:false` rebuild **overwrites the committed `.pr`** — always `cp` it aside
  first and restore after, so a diagnostic run never lands a bad `.pr` in git.
- Stale-binary trap: `plang --test`/`build` use the pre-built
  `PlangConsole/bin/Debug/net10.0/plang`. Rebuild (`dotnet build PlangConsole`)
  before trusting any result.

## Decision summary

| What you see | Origin | Fix surface |
|---|---|---|
| Planner set wrong | #1 | `Plan.llm`, planner teaching |
| Planner right, user-msg sections populated, output wrong | #2 | `Compile.llm`, `os/system/modules/<m>/<action>.{notes,examples}.md` |
| Planner right, user-msg sections **empty** | #3 | templates + `Fluid.cs` binding (native collection / `JsonNode` not Fluid-readable) |

See also: [`understanding-the-builder.md`](understanding-the-builder.md) §12 (traces
& `--debug`), [`building_plang_tests.md`](building_plang_tests.md) (build/test
process), [`debug.md`](debug.md) (full `--debug` property bag).
