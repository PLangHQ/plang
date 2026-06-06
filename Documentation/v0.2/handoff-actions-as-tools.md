# Handoff — Actions as Tools, and Building Goals through the LLM

**Branch:** `runtime2-llm-actions-as-tools` (cut from `runtime2`)
**Author of design:** Ingi + Claude (design conversation, 2026-06-06)
**Audience:** architect — this is a design brief to turn into an implementation plan, not a spec to implement verbatim. Decisions marked **SETTLED** were made deliberately in the design conversation; **OPEN** items need your judgement; **DEFERRED** is explicitly out of v1.

---

## 1. The core idea

`llm.query` already supports tools — but only **goals** (`Tools` is
`Data<List<GoalCall>>`, see `PLang/app/module/llm/query.cs:43`). The realization:
**every `module.action` is also a tool.** The builder already turns intent into
`module.action(params)` at *build time* via `Module.Schema`
(`PLang/app/module/this.cs:23` — "What every action looks like, for the LLM").
This feature exposes that same catalog to `llm.query` at **runtime**, so the LLM
can drive the whole PLang tool chest, not just pre-written goals.

Two capabilities fall out of one mechanism:

- **Execute** — the LLM's plan runs immediately through the normal Action pipeline.
- **Build** — the same plan is *written to disk* as goals (and, later, assets).

Build is one altitude above execute, but the **same loop**: an agentic `llm.query`
whose tool chest includes a goal-authoring tool. Execute emits actions → pipeline;
build emits goals → disk. A goal is emitted *by* an action, so the level-up is one
tool, not a new subsystem.

---

## 2. What already exists (do NOT rebuild)

The agentic tool loop is **already built for goals** — this feature widens what a
tool can be, it does not invent the loop:

- `query.Tools : Data<List<GoalCall>>?` — tool set (`query.cs:43`)
- `query.MaxToolCalls` — loop bound (`query.cs:91`)
- `query.OnToolCall` callback — fires before/after each tool exec with
  name/args/status/result (`query.cs:46`)
- `ToolCall` — `{Id, Name, Arguments(json)}`, currently resolves `Name` → a
  matching `GoalCall` (`PLang/app/module/llm/ToolCall.cs`)
- `Module.Schema : app.builder.type.@this` — the action catalog already rendered
  for an LLM (`PLang/app/module/this.cs:23`)
- `Module.GetCodeGenerated(action) → (ICodeGenerated?, IError?)` — dispatch
  resolution (`PLang/app/module/this.cs:107`)
- `validate` exists as PLang goals: `os/system/builder/BuildStep/Validate.goal`
  **and** `os/system/builder/BuildGoal/Validate.goal`
- `formal` notation + its 1:1 contract with `actions[]`:
  `Documentation/v0.2/formal-plang.md`

---

## 3. Execute mode — the catalog handshake

Progressive disclosure (standard, keeps tokens bounded):

1. Send the **module list** (`module.description` → list of `module.action`).
2. LLM asks for **detail** on a subset → send each action's
   `{description,notes,examples}.md` + signature (already in `Module.Schema`).
3. LLM returns `[{module, action, parameters}, ...]` — which **is** a Step/Action
   list (an ad-hoc inline goal).
4. The plan runs through the **normal Action pipeline** as the app-level
   **service actor**.

### Trust model — SETTLED

Trust is on the **actor**; `module.action` is the **permission namespace**
(wildcards free: `filesystem.*`). The tool selector and the permission grant
**collapse into one namespace** — granting the service actor scopes the chest.

Three tiers keyed entirely on the actor's grants:

1. **Not granted** → action is invisible (never enters the catalog; LLM can't plan it).
2. **Granted, low-stakes** (`filesystem.read`, `output.write`) → runs silently.
3. **Granted, sensitive** (`filesystem.delete`, `environment.run`) → runs through
   the pipeline, but `AuthGate` escalates: pauses and asks the system/human via the
   normal interaction channel before proceeding.

- The **service actor exists at the app level** (Ingi confirmed). Plans run as it.
- **OPEN — tier-3 policy lives on action or on grant?** Leaning **grant**
  (`filesystem.delete:confirm` vs `:auto`) so the action stays dumb and policy
  lives on the actor where trust belongs. See the separate `[Permission]` decision
  in §6.

---

## 4. The execution trace — `%x!tools%`

The result `Data` carries the trace on its meta plane (`!`), distinct from domain
props (`.`):

- `%x%` → the run's return value
- `%x!tools%` → list of executed tools (action or goal), each entry **holding its
  CallStack frame** — NOT a flattened summary
- `%x!tools[0].callstack%` → drill into depth, cause links, audit, timing
- Recursive **for free**: a tool that is itself a goal running `llm.query` already
  nests its sub-frames, because the CallStack nests.

### What the CallStack frame already carries

`PLang/app/callstack/call/this.cs`: `Action` (module/action/params), `Caller`,
`Children` (nesting), `StartedAt`/`CompletedAt`/`Duration`, `Diffs`, `Errors`,
`Tags`, `SnapshotChain()`.

### The one gap — SETTLED as the fix

On **success** the frame does not keep the result. `call.ExecuteAsync`
(`this.cs:229`) returns `result` but only stores `result.Error` into `Errors`.
Add:

```csharp
public data.@this? Result { get; private set; }
...
Result = result;   // in ExecuteAsync, both success and error branches
```

Then `%x!tools%` is a **pure projection over this query's sub-frames**
(`call.Children`) — no parallel list on the handler (avoids OBP smells #3
mutate-there and #6 flat-copy).

### Lifecycle catch — flagged for the architect

Frames are `await using` and **dispose the moment the call returns**
(`action/this.cs:317`). So `%x!tools%` cannot hold live frames — it must be a
**snapshot taken as the plan finishes**. Snapshot machinery exists
(`this.Snapshot.cs` on both callstack and call, used for wire-serialize). Work:
(1) retain `Result` on the frame, (2) make `Result` ride the snapshot, (3)
`llm.query` snapshots its sub-frame chain at the end onto `%x!tools%`. The
"if debug enabled" gate decides whether the snapshot is taken — keep it always-on
for agentic runs since the trace is the whole point there.

---

## 5. Build mode — building goals (and projects) through the LLM

### Same mechanism, different sink — SETTLED

Build = an **execute loop whose tool chest includes a goal-authoring tool**. No
separate build engine.

Two planes during a build:
- **Product plane** — goals being written. Inert text on disk. **No trust** (Ingi:
  "trust is not relevant on build, it's just saving, not running").
- **Process plane** — the LLM running actions to *do* the building (`input.read`
  to ask the user, `filesystem.list` to inspect, `goal.create` to save). This *is*
  execution, gated normally — but the actions are tame.

### `goal.create` — SETTLED

- Name chosen over `llm.create`/`goal.author` (verb belongs on the `goal`
  collection, next to `goal.call` at `PLang/app/module/goal/call.cs`).
- **PLang-backed, not C#:** resolves to `system/module/goal/create.goal`. It writes
  step text to a `.goal` under the target dir (via `filesystem.write`).
- **Generalization this forces:** *an action can resolve to a PLang goal, not only
  a C# handler* — i.e. userland modules written in PLang.

### The build loop — SETTLED shape

```
manifest = llm(intent, catalog, file-tree)     # [{name, path, purpose}] — FIRST
for goal in manifest:
    steps  = llm(goal, catalog, manifest)       # [{text, formal, actions}]
    result = validate(steps)                     # action exists? params bind? + fill pr structure
    while result.errors:
        steps  = llm(repair, result.errors)      # batched holes
        result = validate(steps)
    save(goal, result.pr)                        # goal.create
```

Decisions settled:

- **Manifest first** — not incremental "next goal until empty." Gives a stable
  plan AND lets forward `call X` references validate against the manifest before X
  exists on disk.
- **`actions` stays authoritative and LLM-authored.** `formal` is **not complete**
  and there is **no formal parser yet** (Ingi confirmed) — so we cannot derive
  `actions` from `formal`. The LLM emits `{text, formal, actions}`; `validate`
  works off `actions`. `formal` rides along as the commit-before-serialize scaffold
  + audit line, NOT machine-checked against `actions` (this is why the `formal`
  ↔ `actions` drift in `formal-plang.md` §7 survives).
- **validate is per-step**, folded over the goal:
  `collect(validate(step) for step in steps)`. (`BuildStep/Validate.goal` is the
  unit; `BuildGoal/Validate.goal` already exists too — reconcile which orchestrates.)
- **Errors block the save; unresolved `call` to a not-yet-built goal is a
  *warning*** (info to the LLM), not an error. Today such a `call` already produces
  a warning — feed it back. All errors + warnings batched into **one** round-trip:
  `"step 1: missing Path\nstep 2: must be int\nstep 4: warning — call AddTodo not built yet"`.
- **Priming = structure, not content.** Give the LLM the action catalog + file tree
  + each goal's one-line purpose. It reads a specific goal body on demand via
  `file.read` (progressive disclosure — same as execute mode). Do **not** dump file
  contents.
- The whole orchestrator (manifest → loop → validate → save) is itself a **PLang
  goal**.
- `%x!tools%` (§4) covers a multi-goal build for free — each authored goal + each
  user question is a child frame.

### Permission composes — SETTLED

`[Permission(...)]` (see §6) is declared only on **C# leaf actions**. A PLang-backed
composite (`goal.create`) needs no attribute — its trust profile is the **union of
the actions it calls** (`filesystem.write`, …), surfaced through the same AuthGate.
**Trust is declared at the bottom and bubbles up through composition.**

### Build is the *safer* mode for sensitive work — SETTLED

Execute leans on runtime gates (per-run y/N). Build produces a durable artifact you
**review and sign before it ever runs** — you read `filesystem.delete` in the goal
text, sign once, trusted thereafter. No per-run interruption.

### Self-amplifying — note

A goal authored 3 steps ago is callable as `call X` in the same session, because
authored goals enter the catalog. The tool chest grows itself.

---

## 6. The `[Permission]` attribute — SETTLED idea, needs spec

`[Permission("low|medium|high|max")]` on **C# action handler classes**. The source
generator reads it (mirror how `[IsNotNull]` / `IRawNameResolvable` already flow
Discovery → `ActionClassInfo` → Action emitter). At dispatch, the pipeline compares
the action's level against the service actor's grant and returns an **ask**
(interaction channel) when the action's level exceeds what the actor holds.

- Only on C# **leaves**. PLang composites inherit (see §5).
- **OPEN:** does the *attribute* declare sensitivity, or does the *actor's grant*?
  Design conversation leaned grant-side for tier-3 (`:confirm` vs `:auto`) — but the
  attribute may still set a floor. Architect to reconcile attribute-level vs
  grant-policy.

---

## 7. DEFERRED to a later version — companion assets

Some actions reference build-time assets that must be materialized — e.g.
`ui.render 'todolist.html'` needs the HTML generated and saved. **Explicitly out of
v1** (Ingi: "defer it for the first version"). Recorded so it isn't re-derived:

- Clean design (not v1): a per-action **build-time companion**
  `<module>/<action>.build.goal`, convention-discovered like `.description.md`. The
  build loop runs it after validate with the action's resolved params + goal
  context (path **and** the render's data shape, so the template matches runtime
  variables).
- **Literal path only** — `ui.render 'x.html'` is extractable; `ui.render %file%`
  is not → warning, generate manually.
- Avoid special-casing `ui.render` in the loop — the action owns its own build
  behavior, just as it owns its runtime behavior.

First version ships **goals-only**, no asset generation.

---

## 8. Net scope for v1

Net-new primitives are small — most of the loop reuses what exists:

1. **`Result` on the CallStack frame** + ride the snapshot (§4) — small, enabling.
2. **`%x!tools%`** projection over query sub-frames (§4).
3. **Widen `llm.query` tools** to accept action/module selectors, not only
   `GoalCall` (§3) — feed `Module.Schema`, progressive disclosure, `ToolCall.Name`
   resolves to `module.action` as well as a goal.
4. **Service-actor dispatch + permission namespace** (§3) + **`[Permission]`**
   attribute & generator wiring (§6).
5. **`goal.create`** as a PLang goal (`system/module/goal/create.goal`) +
   **action-resolves-to-goal** dispatch in `Module.GetCodeGenerated` /
   `Module.Schema` (§5). *This is the load-bearing generalization — consider landing
   it as its own step ahead of the build loop.*
6. **The build orchestrator goal** (manifest → loop → validate → save) in PLang (§5).

### Suggested sequencing question for the architect

`goal.create` being PLang-backed means *an action can resolve to a goal*. That
touches `GetCodeGenerated` and `Module.Schema` and is the riskiest generalization.
Recommend deciding early whether it lands as its own branch/step **before** the
agentic build loop, or together with it.
