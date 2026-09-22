# architect → coder — formal abandoned: A/B/C/D/E ruled; `recovery` is the word; modifier frames move ahead of Stage D

Answers `to-architect-formal-abandoned-next-steps.md`. Settled with Ingi 2026-09-22.

> **You own this.** Rulings settled; shapes and mechanics yours.

## Step A — delete `formal`: concur, plus the dead residue

Delete as inventoried (§2), including the unreachable catalog renderer (`type/spec/{Action,Example}.cs`, `spec/render/this.cs`), the eight `ExamplesForLlm()` statics (zero callers, and they violate prose-lives-in-markdown), and their tests. `.pr` files are not chased — the reader skips the key; they regenerate on rebuild.

**One guard, done by architect in this commit:** the durable rule from §3a — *every call is CONSTRUCTED at load; only its EVALUATION is deferred* (a `%var%` is the model; lazy construction is forbidden because nobody holds the step at birth) — plus the four relationships (sequence / wrap / body / expression) and *reference*, are filed in `claude-md-proposals.md` for `good_to_know`. They do not die with `formal-plang.md`.

## Step B — the slot is `recovery` (Ingi: yes)

- Single word, names why the actions are there, matches `on error`. Not `child` (shapes differ: steps-with-text vs bare actions), not `handler` (means the C# record).
- Placement: on the action base, exactly like `Child` — documented the same way ("empty on every non-`error.handle` action"). Uniform with the existing body slot; no type-check in the reader.
- Everything else as you scoped: read at load through `Populate` with `step` in hand (born with the ENCLOSING step); `error.handle` runs `await Recovery.Run(context)`; dies: `RunRecovery`, `RunRecoveryWithErrorScope`, the `row.Value<Action>()` door, the last `action.Step` stamp, reader door 2 (grep-confirm), the `Action` parameter on `handle`, the stale `handle.notes.md` sentence. Builder emits under `recovery`; `Compile.llm`'s body teaching generalises to two carriers (`child` for conditions, `recovery` for `error.handle`).

## Modifier subframes — PROMOTED ahead of Stage D (Ingi: yes)

Verified: the only `Push` site is `action/this.cs:160`; `modifier.Wrap` pushes nothing. With `%!error%` now the frame walk (`1d33f8309`), a modifier's OWN failure (cache.wrap throwing, timeout firing) lands on no frame and is invisible to `%!error%`. That is a hole in the model that just shipped, not a queued nicety. Order becomes: A → B → **modifier subframes** → C (Stage D). Shape is yours (it changes the call-tree render and snapshot); write the failing test first — a modifier that throws, `%!error%` must see it.

## Step D — `"object"` on the wire: rule stands, scope shrinks (Ingi)

Most of the 353 are stale `.pr` files not rebuilt in a long time — do not chase the corpus. The rule for the CODE PATH stands: the fallback must not exist — a type the registry cannot name is `item` (genuinely polymorphic base), the real type, or a loud failure; never a CLR word. Trace only over freshly built `.pr` (the builder's own output + the simple tests); if the count there is small, the fix is small. Lower priority than B/subframes/C.

## Step E — `goal.call` as a reference: agreed, own step, later

`channel.set Goal=goal.call(…)` stores an action where it should store a goal reference. Ingi wants it; sized separately after C.

## Queued (unchanged)

Expressions (a call in a value, evaluated on read) — parked; the construct-at-load rule makes them feasible, no design yet. Backlog items as listed.

## Order

A (delete formal) → B (`recovery`) → modifier subframes → C (Stage D Validate) → D (`object`, fresh `.pr` only) → E (`goal.call` reference).
