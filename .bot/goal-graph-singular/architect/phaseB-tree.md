# Phase B, redefined — the graph is a tree (settled w/ Ingi 2026-07-20)

**This replaces the old Phase B** (`items-answer.md`'s "delete `steps.@this`, re-home the loop"). The loop question dissolves: there is no step loop. Nesting + `step.Run` fire-or-fall-through replaces `skipBelowIndent` entirely, and the collection `steps.@this` becomes a plang `list<step>` — which the tree needs anyway. So the tree *is* Phase B done right; nothing is left over to do separately.

> **You own this.** The shapes, `.pr` layout, and dispositions below are the ruling. Bodies, the builder prompt/schema, the exact fold pass, naming inside — yours. Everything marked NEW is a suggestion of shape, not final text. The one thing that is NOT yours to change silently: `child` on the control-flow action, `list<step>` everywhere, `Decision` retires — that's the settled structure. If a trace shows it can't hold, come back, don't route around it.

## Why

Phase B blocked on "where does the step loop live" — `steps.RunAsync`'s `skipBelowIndent` had no OBP-correct home once the collection deletes (goal isn't the orchestration owner; Ingi rejected folding it there). The answer is that the flat-list-plus-indent is a **flattened tree pretending to be a sequence**. Make it a real tree and the skip-state vanishes: a condition owns its body as `child`; running a condition either enters its `child` or falls through to the next. One model for both forms of nesting (inline `if/elseif/else` and indented sub-steps), `Decision` retires, and the "2 ways of doing conditions" Ingi flagged collapses to one.

## The model

Three collections, all plang lists, `list<step>` reachable at two homes:

- `goal.Step` : `list<step>` — the goal's top-level steps (a flat sequence; no indent among them).
- `step.Action` : `list<action>` — a step's actions (one action, or an `if`/`elseif`/`else` chain of condition actions).
- `action.Child` : `list<step>` — a **control-flow action's body**. The steps that run if that condition fires. Empty/absent on a non-control-flow action.

`child` lives on the **action**, not the step. Both nesting forms land here:
- inline `if %x% set %a% = 1, else …` → the step's `action` is `[condition.if(child:[…]), condition.elseif(child:[…]), condition.else(child:[…])]`.
- indented block (`if %flag%` then `- set …` lines) → the `condition.if` action's `child` holds those steps.

`goal.Child` (sub-goals, `list<goal>`, the `Goals→Child` rename) is a separate, existing thing — untouched. Note the parallel: every node's nested peers of its own kind are its `child` (a goal's children are goals; a control-flow action's children are steps).

### Running it — each owner runs its own sequence

There is no shared "step runner" and no bespoke collection type. The owner of a `list<step>` runs it:

- `goal.Run` (was `RunAsync`) — keeps the goal lifecycle (events, call frame, cycle guard, return-depth, `goal/this.cs:247`). The one changed line: `await Steps.RunAsync(context)` becomes a **trivial sequence-run** over `Step` — `foreach step: r = await step.Run(context); if r.ShouldExit() || r.Returned break;`. No skip state (the tree holds the gating). "A goal runs its steps in order" is honest goal work; the *complex* control flow is gone from here.
- `step.Run` (was `RunAsync`) — keeps the step lifecycle (before/after events, the exception→ServiceError catch, `step/this.cs:148`). Runs the step's action(s). For a condition chain it does **fire-or-fall-through**: evaluate the conditions in order; the first that fires runs its `Child` and the rest are skipped; none fires → the step is a no-op (unless there's an `else`). A single non-condition action just runs.
- the condition action owns running its own `Child` on fire (a sequence-run over `Child`, same shape as goal's over `Step`). `if`/`elseif`/`else` share it via their common `condition` base.

The 4-line sequence-run appears at `goal` (over `Step`) and at the `condition` base (over `Child`). That is **not** a stray helper — each is an owner running *its own* list, which is exactly OBP ownership. Do not extract it into a shared runner (that resurrects a `step.list` behavior type Ingi rejected); do not push it onto the generic `list` (it's not the generic list's job to know step lifecycle).

> **OPEN — confirm before building `goal.Run`.** The above has `goal.Run` own a trivial `foreach` over `Step`. The alternative is fully step-driven — `step.Run` returns the next step and a 3-line trampoline drives it (zero loop in goal). My lean is the trivial foreach: the skip-state is already gone, so what remains in goal is just "run my steps in order," which is legitimately goal's job, and it avoids threading a `Next` linkage through every step. Flagging because it's the last remnant of the loop-owner question Ingi opened. Coder: do NOT pick silently — this one comes back to architect + Ingi.

## The `.pr` shape

`indent` is **gone** — nesting is the structure. Keys are the singular sweep (`step`/`action`/`name`/`child`, `line`). Every step has `text` (LLM-authored for synthesized body-steps; see Builder). Two worked cases:

**Inline `if/elseif/else`** (`if %x% > 10 set %a% = 1, else if %x% > 5 set %b% = 2, else set %c% = 3`) — one step, branches on its condition actions:

```json
{ "index": 1, "text": "if %x% > 10 set %a% = 1, else if %x% > 5 set %b% = 2, else set %c% = 3",
  "action": [
    { "module": "condition", "name": "if",
      "child": [ { "index": 0, "text": "set %a% = 1", "action": [ {"module":"variable","name":"set"} ] } ] },
    { "module": "condition", "name": "elseif",
      "child": [ { "index": 0, "text": "set %b% = 2", "action": [ {"module":"variable","name":"set"} ] } ] },
    { "module": "condition", "name": "else",
      "child": [ { "index": 0, "text": "set %c% = 3", "action": [ {"module":"variable","name":"set"} ] } ] }
  ] }
```

**Indented block** (`if %flag%` then two indented `- set` lines) — one step, one condition action, its `child` holds the block:

```json
{ "index": 2, "text": "if %flag%",
  "action": [
    { "module": "condition", "name": "if",
      "child": [
        { "index": 0, "text": "set %innerRan% = true",         "action": [ {"module":"variable","name":"set"} ] },
        { "index": 1, "text": "set %innerValue% = \"executed\"","action": [ {"module":"variable","name":"set"} ] }
      ] }
  ] }
```

Same shape — the block children already had their own `text` (they were written lines); the inline children get `text` from the LLM. Byte-identical modulo keys is not the golden here (the shape genuinely changes) — the golden is a semantic round-trip (write→read→write stable) + preserved runtime behavior.

## Leaf trace + call-site dispositions

| incumbent | today | disposition |
|---|---|---|
| source parse `goal/this.cs:446-463` | `indent = leadingSpaces/4`; flat `Steps.Add` | build the tree: a deeper-indented step's disposition is decided **post-compile** (indent is known now, the condition action is known after compile). `indent` field removed. |
| `steps.RunAsync` `steps/this.cs:105` (`skipBelowIndent`) | the step loop | **deleted.** The trivial sequence-run moves to `goal.Run`; the gating moves into `action.Child` + `step.Run` fire-or-fall-through. |
| `steps.HasIndentedChildren` `:55` | indent lookahead | **deleted** — the tree encodes it structurally. |
| `steps.Nest` `:64` / `step.Nest` | modifier nesting per step | re-home the per-collection loop; `step.Nest` (modifier fold onto the action) is unaffected by the tree. Verify it runs over the tree (recurse into `child`). |
| `steps.Merge` `:79` / `step.Merge` `step/this.cs:193` | flat text-match | **structural/recursive** (Ingi settled): match a parent by `text`, its `child` by position. Child `text` is display/intent, not the match key (LLM text can reword). |
| `condition.if.Orchestrate` `condition/if.cs:71` | walks `Decision` over the flat action array | **deleted.** The tree already separates branch bodies; `step.Run` coordinates fire-or-fall-through; `condition.if.Run` runs its own `Child` on fire. Keep the evaluation (Evaluator, truthiness door, `Negate`). |
| `Decision` type `condition/decision/this.cs` (`Of`/`IsHead`/`Head`/`Split`/`Chain`, `Branch` record) | groups the flat action array into branches | **retires, whole.** Nothing to group — the tree is the grouping. The four methods that just left `actions.@this` this branch die entirely, not re-homed again. |
| `test/discover.cs` reads `Decision.Of(step.Actions).Chain` | seeds coverage | reads the tree instead: the step's condition-action chain + each `Child` presence. `RecordBranchChain(site, chain)` stays; its **source** changes from `Decision` to the tree. |
| `test/Coverage.cs:48` `RecordBranch` / `:81` `RecordBranchChain` | the coverage surface | **stays.** `branchIndex` = position of the fired condition action in the chain; the chain = the step's condition actions. Only the producer changes. |

## The builder change (the new surface — coder owns; two producers)

Located, not fully traced — this is yours to design; entry points:

1. **Indented blocks → deterministic, post-compile.** `goal/this.cs:446` parses indent today; the planner (`os/system/builder/BuildGoal/Plan.goal`) and per-step compile produce actions. After compile, a deterministic pass folds a deeper-indented step into the preceding step's **control-flow action** `child`. No LLM. `indent` stops being a persisted field; it's transient parse state consumed by the fold.
2. **Inline `if/elseif/else` → the LLM.** The compile that today emits the flat `[if, body, elseif, body, else, body]` action array instead emits condition actions each carrying `child: [body step(s)]` with **LLM-authored `text`** per branch (Ingi: the line is freeform, only the model parsing it knows each branch's boundary — a deterministic splitter can't). This is the schema + prompt + examples change, and the honest eval risk. Own the golden set.

> Verify while you're in there: does indentation *only* ever follow a control-flow action (condition)? `foreach` calls a goal (no sub-steps, per CLAUDE.md). If a non-control-flow step can have indented children, decide whether that's an error or needs a home — do not assume; check real goals + the parser.

## Demolition worklist (by gate)

**Dies with the tree (this increment):**
- `goal/steps/this.cs` (`steps.@this`) — whole class: `RunAsync`/`skipBelowIndent`, `HasIndentedChildren`, `Nest` (loop), `Merge` (flat).
- `condition/decision/this.cs` — whole: `@this`, `Of`, `IsHead`, `Head`, `Split`, `Chain`, `Labels`-inlined-into-`Of`, the `Branch` record.
- `condition/if.cs` `Orchestrate` — the action-array walker.
- `step.Indent` property + `goal/this.cs:447` indent computation as a *persisted* field (transient-only during the fold).
- `GoalSteps` alias (`GlobalUsings.cs`), `goal.Steps`' `steps.@this` type → `list<step>`.
- wire `indent` key; the `Decision`-sourced `branchChain` computation (chain now read off the tree).

**Stays (do not over-delete):**
- `goal.Run` lifecycle: events, `CallStack.Push` goal-entry frame, cycle guard (`ContainsGoal`), return-depth (`goal/this.cs:247+`). Only the `Steps.RunAsync` seam changes.
- `step.Run` lifecycle: before/after step events, the exception→`ServiceError` catch (`step/this.cs:148+`).
- `condition.if` evaluation: `Evaluator`, the truthiness door (`ToBooleanAsync`), `Negate`.
- `test/Coverage.cs` `RecordBranch`/`RecordBranchChain`/`BranchChains` — surface unchanged.
- the goal/step/action `ITypeReader`s + `Output` (extended to recurse `child`, not replaced).
- `goal.Child` (sub-goals, `list<goal>`) — separate concept.

## OBP validation (new/changed surfaces)

| surface | shape | verdict |
|---|---|---|
| `action.Child` | `list<step>` on control-flow actions | clean noun. Alt `Body` (semantic) — I lean `Child` for parity with `goal.Child` and the tree framing; flag if you prefer `Body`. |
| `goal.Run`, `step.Run`, `condition.if.Run` | one honest verb; `Async` dropped (everything is async) | clean. |
| the fold pass | deterministic post-compile tree assembly | **name it carefully** — not `BuildTree`/`FoldSteps` (verb+noun). It's the compile producing its own shape; likely no standalone named method at all (the parse/compile owns it). Do NOT introduce a `TreeBuilder`. |
| sequence-run (goal over `Step`, condition over `Child`) | a `foreach` on each owner | clean — ownership, not a shared helper. No extraction. |

## App-model plang-types audit

- `goal.Step` : `list<step>` ✓ · `step.Action` : `list<action>` ✓ · `action.Child` : `list<step>` ✓ (kind-as-element `{list, kind:step}`).
- `branchIndex` : `number` ✓ (stays).
- `indent` : was `int` — **removed**, not converted (the tree replaces it).
- every leaf a plang type — no raw CLR leaking onto the wire faces.

## Acceptance

1. Tree `.pr` semantic round-trip: write → `ITypeReader` read → write stable, for both the inline and block forms.
2. Runtime behavior preserved: `if/elseif/else` fires the right branch; `branchIndex` + coverage output unchanged vs the flat baseline; sub-step block still gated by its condition.
3. Builder goldens: one inline `if/elseif/else` + one indented block, LLM emits the tree with per-branch text; the deterministic fold produces the block tree.
4. `Decision` gone — grep-gate `condition.decision` / `SplitAtConditions` / `ComputeBranchChain` → zero.
5. `indent` / `skipBelowIndent` / `HasIndentedChildren` → zero in production.
6. Bootstrap: the ~11 hand-edited builder `.pr` files re-nested **structurally** by hand (Ingi-permitted, this branch); everything else regenerates from source.
7. `goal.Run` run-drive: whichever of trivial-foreach / step-driven Ingi confirms (OPEN above).
