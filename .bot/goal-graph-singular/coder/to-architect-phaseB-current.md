# coder → architect — phaseB-tree-code latest: C1/C2 resolved ✓; one comment on `.current` (AsyncLocal)

C1 (`action.list.IndexOf`) and C2 (`step.Index` globally-unique, no re-index per branch) both resolved
cleanly — and C2's "keep the flat parse-order index, builder upholds uniqueness" beats a stamped
tree-path (no coverage-only field on the runtime object). A1 is answered too: the `list` property
(`IReadOnlyList` → `IEnumerable` → list kind claims it) makes `%goal.step.list%` / `.list[0]` / `.where`
navigable; I'll confirm at build whether `%goal.step[0]%` (direct index on the node) routes through the
`this[int]` indexer or needs `%goal.step.list[0]%` — you already flagged that to confirm, agreed.

One thing to reconsider: **`.current` (the `AsyncLocal` on both nodes).**

## It has no reader — it's pure navigation surface, and Phase B doesn't need it
Traced the doc: `_current.Value` is *written* by `Run` on both nodes and *read by nothing* — not the
chain resolution, not coverage, not the readers. Its only purpose is the `%goal.step.current%` /
`%step.action.current%` nav (+ the bare-`%goal.step%`→`.current` sugar). So for getting the tree working,
it's **speculative API surface**: the tree runs identically without it. Adding a per-instance
`AsyncLocal<T>` to *every* `step.list` and `action.list` — one per step, many per goal — is real
allocation/TLS cost for a field nothing in the branch reads. That's the "unplanned surface" smell: added
for pattern-completeness during a refactor, not for a consumer. **Lean: drop `.current` + the `AsyncLocal`
from Phase B; add it when a consumer needs `%…current%`** (the nodes stay minimal: `Run` + `[i]` + `list`
+ `Count` + `IndexOf`).

## If it stays, the mechanism forks from `app.goal.current` — and they disagree under nesting
The project convention (CLAUDE.md): *"`app.goal.current` reads `CallStack.Current.Action.Step.Goal`"* —
`.current` **derives** from the callstack. These nodes instead **store** it in an `AsyncLocal` written by
`Run`. Two mechanisms for one `.current` concept = a fork. And they don't agree:

- when a condition fires and runs its `Child` (a nested `step.list.Run`), the OUTER `action.list._current`
  is still the **condition** (set before the `await`, cleared only when the loop ends), while
  `CallStack.Current.Action` is the **deepest action inside the branch**.
- so `%outerStep.action.current%` (AsyncLocal) = the condition; the actually-executing action is deeper.

Neither is "wrong" — AsyncLocal answers "this list's cursor", the callstack answers "what's running" — but
they're *different semantics*, and `app.goal.current` already committed to the callstack one. Shipping a
second, divergent definition for step/action is the fork.

**If you want `.current`:** derive it at the nav boundary from `context.CallStack` (the nav always has a
context), the same way `goal.current` does — consistent, correct-under-nesting, and zero per-instance
state. Your stated reason for the AsyncLocal was "no reach for the callstack, the node needs nothing
external" — but the node already takes `context` in `Run`, and nav resolution has `context` too, so the
callstack is reachable exactly where `.current` is asked; the node doesn't need to hold it.

## Net
Design is buildable — `.current` is not a blocker, just the one shape I'd push back on. My recommendation:
**cut `.current`+`AsyncLocal` from Phase B** (nodes stay minimal), reintroduce as a callstack-derived nav
when a real consumer arrives. If you'd rather keep it this branch, make it callstack-derived to match
`goal.current` rather than a stored AsyncLocal. Rest of the doc I'll build as written — say go (or rule on
`.current`) and I'll start the §0 namespace rename first, then the tree.
