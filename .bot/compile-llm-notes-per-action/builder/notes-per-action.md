# Per-action Notes in the Compile prompt

**For:** architect (design); coder will implement after the design is set.
**From:** builder bot.

This document is intentionally **the idea and the why**, not the how. Pick the data shape and the implementation seam — I have opinions but want yours to win.

## The problem

The Compile-step LLM system prompt currently carries ~22 KB. A large fraction of it is action-specific teaching that's relevant only when a particular action is in the planner's set, yet every step compile sees every rule. Concretely, the sections in `os/system/builder/llm/Compile.llm` that read like "rules about *this* action":

| Section | Relevant only when planner picked … | Approx size |
|---|---|---|
| `error.handle` recovery semantics | `error.handle` | ~1.5 KB |
| `"on error call X"` callback-vs-modifier | `error.handle` | ~0.4 KB |
| `"is not empty"` operator | `condition.if` with `isnotempty` operator | ~0.3 KB |
| `foreach` — Collection-only, `%item%` | `loop.foreach` | ~0.5 KB |
| `call X, name=value` parameter passing | `goal.call` | ~0.5 KB |
| `goal.call` payload — name field is goal name | `goal.call` | ~0.3 KB |
| `llm.query` `system=/user=` shorthand | `llm.query` | ~0.3 KB |
| Channel routing inside `output.write` rules | `output.write` | ~0.4 KB |
| `AsDefault` flag / `code.setDefault` distinction | `variable.set` | ~0.5 KB |

That's ~5 KB of action-specific text that fires on every step compile regardless of relevance. For a step like `set %x% = 'hello'` (planner: `["variable.set"]`), the compile LLM reads `error.handle.Actions` nesting rules, `loop.foreach` iteration rules, `llm.query` shorthand rules, and `goal.call` payload rules. None of those apply.

## Why this matters beyond size

Two effects compound:

**1. Token cost.** Self-explanatory; ~5 KB × every step compile × every build.

**2. Rule density triggers drift.** We have direct evidence the LLM's adherence to its own rules degrades as the prompt thickens with competing constraints. Two recurring drift cases on the current branch:

- `output.write` for `write out %message%` (no channel clause in step text) compiled with `formal='output.write(Data=%message%, channel=%!data%)'`. The JSON `parameters` correctly omitted channel — the schema constraints saved it — but formal, which is free text, sprinkled `%!data%` into the optional `channel` slot. Tightening the formal-mirroring rule in the cross-cutting system prompt fixed *this* case but didn't address the family.
- `assert %message% equals 'hello plang'` (no custom error message in step text) consistently compiles with `Message='hello plang'` duplicated from `Expected`, and `formal='assert.equals(Expected=%!data%, ...)'` disagreeing with the JSON's actual `Expected='hello plang'`. The cross-cutting rule "NEVER use `%!data%` to fill an optional parameter that wasn't named in the step text" is in the prompt and is being ignored.

Adding more cross-cutting prose to fix per-action misbehavior makes the prompt thicker, which makes the misbehavior more likely on the next action. The cycle isn't budget-bounded by anything in the current shape.

**3. The right teaching lives with the right action.** "Omit `Message` unless step text names a custom error message" is a fact about `assert.*`. It has no business being in a section about every action. Catalog data should travel with the catalog entry it constrains.

## The proposal

Each action in the runtime catalog (`Modules.Describe()` output) gains an optional `Notes` field — short, declarative text that constrains how the LLM should fill that action's parameters when it's compiled. The Notes for an action render in the **user message** (in `stepActionDetails.template`) only when the planner picked that action — never in the system prompt.

That's the whole idea. Two consequences fall out:

- The system prompt loses ~5 KB of action-specific teaching; it keeps only the truly cross-cutting rules (modifier-vs-peer classification, `write to %var%` ⇒ peer `variable.set`, output shape, formal-mirroring rule, error keys, type-name conventions, JSON-shape rule).
- Each step's compile call sees only the Notes for actions actually in play. A `set %x% = 'hello'` step sees `variable.set` Notes and nothing else; an `assert ... equals ...` step sees `assert.equals` Notes (which would contain the "omit Message unless named" rule).

## Migration map (the destinations are the load-bearing part)

Architect's job to decide the storage and renderer; I'm just listing what moves where so the scope is honest:

| Move from `Compile.llm` | To Notes on |
|---|---|
| `error.handle` recovery: Actions list, no duplicate peer, Key/Message filter rules | `error.handle` |
| `"on error call X" → modifier, not callback param` | `error.handle` |
| `foreach` Collection-only, `%item%` is auto-bound | `loop.foreach` |
| `call X, name=value` → `GoalName.parameters` | `goal.call` |
| `goal.call` payload — `name` is the goal identifier | `goal.call` |
| `llm.query` `system=/user=` shorthand | `llm.query` |
| `"is not empty" → isnotempty` operator pattern | `condition.if` (or operator-type description) |
| `output.write` channel routing rule | `output.write` |
| `AsDefault` flag for variable.set; `code.setDefault` is unrelated | `variable.set` |
| `Message` stays out unless step text names it (NEW — fixes the current drift) | `assert.equals`, `assert.contains`, `assert.notEquals`, etc. — all `assert.*` |

That last row is something we don't have today and should add as part of this work — it's a concrete bug to fix, not a wish.

## What stays in `Compile.llm` (the cross-cutting kernel)

These rules apply to every action; they belong in the system prompt:

- Job description, output shape (`formal` + `actions[]` + `errors` + `warnings`), formal-mirroring rule
- Type-name conventions (`module` is a single identifier; valid type tokens; `tstring` is a runtime hint)
- Modifier-vs-peer hard classification (only `error.handle`, `cache.wrap`, `timeout.after` are modifiers)
- `write to %var%` always emits a peer `variable.set` (mechanical wrapping pattern)
- Compound conditions split into multiple `condition.if` instances
- JSON values: structured, with `"type": "json"` — not stringified
- Error key vocabulary (`missing-actions`, `actionNotFound`, etc.)
- The general Rules section (omit nullable params, never sprinkle `%!data%`, preserve `%name%` literally, etc.)
- Per-step Type Information and Action Detail location pointer (those live in the user message already)

These are ~15 KB combined. After the migration the system prompt sits at ~15 KB instead of ~22 KB, and each step's user message grows by the Notes for its planner's set (typically 1–3 actions × a few short rules each = ~0.3–1 KB).

## What I want from architect

A design that nails these decisions:

1. **Where does `Notes` live in the catalog?** Author-declared on the action class (attribute, similar to `[Example]` / `[Description]`)? Or a sibling file under `os/system/actions/`? Or both, with one as the canonical and the other as transient overlay?
2. **One blob vs. structured?** Plain markdown blob, or a list of `{rule, optional example}` entries? The latter renders more consistently but is more authoring overhead.
3. **What does it look like in the rendered user message?** A `Notes:` subsection per action under its `Examples:` block in `stepActionDetails.template`, I assume — but confirm.
4. **Modifier actions** (`error.handle`, `cache.wrap`, `timeout.after`) — these aren't directly in `planStep.actions` per se; they show up via the planner's set. Notes on `error.handle` need to render when `error.handle` is in the set the same way as any non-modifier action's Notes. Confirm the existing rendering path handles this uniformly.
5. **Authoring location**. Right now most catalog metadata lives next to the action class in `PLang/app/modules/<module>/<action>.cs` (via `[Description]`, `[Example]`). Notes likely fit the same place. Just confirm direction so coder doesn't have to guess.

## Out of scope (named explicitly so they don't get scope-crept in)

- Adding a *validator* that checks "no extra parameters in formal vs. actions" — separate concern; the formal-mirroring rule lives in the cross-cutting kernel and stays there. Notes don't replace structural validation; they preempt it.
- Migrating Plan.llm rules. The planner doesn't have the same per-action density problem because it just emits names. Leave Plan.llm alone for this pass.
- Changing the way the catalog is loaded into the LLM template. Notes just adds one new field to the action entry; the existing pipeline (`builder.actions` + `summary.md`/`stepActionDetails.template`) renders it.

## Verification once it lands

Two quick checks that prove the work succeeded:

1. **System prompt size on a Tests/Simple step compile drops from ~20.8 KB to ~15 KB.** Trace inspection.
2. **The current drift cases are fixed end-to-end**:
   - `write out %message%` step → `formal='output.write(Data=%message%)'` (no `channel=%!data%`).
   - `assert %message% equals 'hello plang'` → `Message` is OMITTED from `parameters[]` and from `formal`; `Expected='hello plang'` matches between `formal` and `parameters[]`.

If both hold across 3 fresh-cache builds of Tests/Simple in a row, the structural fix is working — drift bounded by what each action's Notes constrain, not by global rule density.

## Why this is the right time

We just landed the typed-returns sweep (commit `c1e7b7090` and surrounding) which collapsed one whole class of recurring regression by structural means (Run() signature is the source of truth, no separate declaration that drifts). The Notes work is the same shape: stop teaching the LLM the same constraints in N+1 places and let each action carry its own rules. The pattern's proven on this branch already; this just extends it.
