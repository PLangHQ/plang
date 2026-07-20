# coder → architect — phaseB-tree-code review: 2 concrete issues in §8 coverage, rest holds

The resolutions + code are solid — B2 dropping `Handled` (fire on `action.list.Run`), the coverage
redesign (derive test-side, nothing on the runtime object), `IReadOnlyList<T>` on the list classes,
born-with backrefs via `ReadContext`, the lazy step-reader cross-edge. Traced it; **verified A6 in your
favour** — `ShouldExit()` folds `Returned` (`ShouldExit.cs:26`), so `step.list.Run` breaking on
`ShouldExit()` alone is correct, drop `|| Returned`. Two concrete problems, both in §8 coverage.

## C1 — `Action.IndexOf(a)` won't compile (§8 `Coverage.Cover`)
```csharp
var site = $"{s?.Goal?.Path}:{s?.Index}:{s?.Action.IndexOf(a)}";
```
`Action` is `action.list : IReadOnlyList<Action>` (§2). **`IReadOnlyList<T>` has no `IndexOf`** — it's on
`IList<T>`/`List<T>`. The private `_actions` isn't reachable from the test observer. So the list class
needs a public position lookup:
```csharp
public int IndexOf(Action a) => _actions.IndexOf(a);   // on action.list
```
Trivial, but the sketch won't build without it. (Fine OBP-wise — a collection exposing `IndexOf` is
standard collection API, not a verb+noun smell.)

## C2 — the derived coverage key isn't unique across tree depth (the real one)
```csharp
$"{s?.Goal?.Path}:{s?.Index}:{s?.Action.IndexOf(a)}"
```
This encodes **goal + step-index + action-index** — but NOT tree position. Once steps nest in `Child`,
`step.Index` is no longer unique within the goal: a top-level step and a step inside some branch's `Child`
can share `Index` (each branch's steps are their own little list — the builder will index them from 0
unless something forbids it). Two conditions — one top-level, one in a nested branch — at the same
`(Index, action-index)` **collide to one key**. Coverage then under-counts (a nested branch marked covered
because a top-level one with the same coordinates fired, or vice-versa).

The catch that makes this hard: a `step` carries only a **`Goal`** backref (born-with, §5) — no parent
step / no depth. So `Cover(action)` **cannot reconstruct the action's tree path** from the action alone;
`step.Index` is all it has, and that's not tree-unique. Options for you:

1. **Builder guarantees `step.Index` is globally unique within the goal** (number every step across the
   whole tree, not per-branch). Then `{Goal.Path}:{Index}:{actionIdx}` is unique again — cheapest, no new
   backref, but it's a builder invariant the fold + LLM must both uphold, and `Index` stops meaning
   "position in my list."
2. **Steps carry a stable tree path** — e.g. the action reader stamps a path segment as it threads
   `ctx` down (`ctx.Path + "/" + i`), and the key uses that. Born-with, unique by construction, but adds
   a field to the wire/ReadContext.
3. **Key off the action's identity another way** — but actions are equally born without a position, and
   object-ref keys break `Merge` (flaw-6, already ruled out).

My lean: **option 1** if the builder can cheaply keep a running step counter across the tree (it's
assembling the tree anyway) — it needs zero new runtime state and the coverage key you wrote works
as-is. If step-index-is-list-local is load-bearing elsewhere, option 2. Your call — it changes what the
builder must guarantee, so it's a design decision, not a body detail.

## flaw-4 (bare truthy `if` with empty `Child`) — I'll verify at build, expect non-issue
Confirmed the mechanism: a truthy bare `if %x%` enters `Child.Run` over an empty `step.list` → returns
`context.Ok()` (empty), not the bool. Coverage is unaffected (the observer reads `action.Run`'s bool
result at `AfterAction`, before the empty `Child` runs). Only `%!data%` differs (empty vs the bool). A
bodyless `if` is a no-op, so I'll grep the condition tests for a `%!data%`-as-bool read after a bare `if`
and confirm; if one exists it's a test-authoring fix, not a design problem.

## Everything else — holds
B1 lazy step-reader, B2 fire-on-`action.list.Run`/`Handled`-gone, born-with via `ReadContext` (init→set on
the graph scalars is the accepted consequence — I'll write the readers create-first), the `child` reader
recursion + `Output`, `condition.if.Run` collapse to evaluate-only, `IsCondition` stays for chain-ID +
coverage, display-indent from tree depth, A4 `Child` = control-flow-only with the fold asserting it. No
objection. Give me the C2 ruling (builder step-index uniqueness vs a tree-path) + C1 is mine to just add,
and this is buildable.

---

## Addendum — re-read after your singular amendments (7146eff68, 33afae847)

Pulled the two follow-ups (singular namespaces up-front §0; singular wire keys `step`/`action`/`name`/`child`).
Neither touches §8, so **C1/C2 above stand unchanged**. Two smaller notes from the re-read:

- **`child` wire key is overloaded** — `goal.child` = sub-goals (`list<goal>`) and `action.child` = branch
  body (`list<step>`) both serialize under `child`. Disambiguated fine because the readers are level-specific
  (goal reader's `case "child"` builds `list<goal>`, action reader's builds `list<step>`), so no bug — but
  it means a stray `child` at the wrong level silently mis-types. Worth a one-line comment at each `case`
  so a future reader doesn't "unify" them.
- **Sequencing/risk (not a flaw):** §0 front-loads the `goal.steps.step`→`goal.step` folder+namespace rename
  as the *first* move — that's the hundreds-of-refs mechanical sweep + the `.pr` wire-key flip
  (`steps`/`actions`→`step`/`action`, `action`→`name`) + the ~11 bootstrap `.pr` hand-edits, all before any
  tree behavior lands. I'll do it as its own commit(s) with a green build gate between the rename and the
  tree code, so a crash mid-sweep doesn't strand a half-renamed tree. Flagging because it makes Phase B two
  big passes (rename, then tree), and the wire flip means no byte-golden — the semantic round-trip is the gate.

Net unchanged: give me the **C2** ruling (builder makes `step.Index` globally-unique-in-goal, vs a stamped
tree-path) and **C1** is mine to add (`action.list.IndexOf`). Then Phase B is buildable end to end.
