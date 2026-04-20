# Codeanalyzer v2 Plan — Re-review of coder fix for v1 findings

## Context

Commit `8a462217` ("Address codeanalyzer v1 must-fix findings") claims to
resolve all four v1 must-fix items. This session re-reviews the coder's
fixes and scans for new issues the refactor may have introduced.

The diff touches 9 files, +181 / −182 LOC:
- OBP owner methods added to `Goal`, `Steps`, `Actions`, `Action`
- `BranchChain.cs` deleted (methods moved onto `Actions`/`Action`)
- `if.cs` `Orchestrate` now delegates to owner methods
- `discover.cs` three walks now use `goal.ForEachAction` + owner helpers
- `run.cs` no-op UserTags loop removed; `IsCondition` / `IsFirstConditionInStep`
  delegation
- `report.cs` `System.IO.Path.GetDirectoryName` → `fs.Path.GetDirectoryName`,
  `BuildJUnit` takes `fs` parameter
- `discover.cs` `System.IO.Path.ChangeExtension` → `fs.Path.ChangeExtension`

## Review approach — five-pass, scoped to the fix

### Pass 0 — Verify each v1 must-fix item was actually fixed

1. **V1#1 System.IO** — grep for `System.IO` across `test/*.cs` and
   `condition/*.cs`. Confirm 0 residual instances. Read the two new call
   sites (`fs.Path.ChangeExtension` in discover.cs:77, `fs.Path.GetDirectoryName`
   in report.cs) and verify that `fs` is in scope and not null at those points.
2. **V1#2 no-op copy-loop** — verify the two-line `foreach ... UserTags.Add`
   in run.cs is gone. Check that `testRun.UserTags` is still populated via the
   normal `childApp.Testing.CurrentTest` → `testRun` alias path.
3. **V1#3 duplicated declared-chain** — verify `if.cs` Orchestrate now calls
   `actions.ComputeBranchChain(myIndex)` and that the implementation on
   `Actions.ComputeBranchChain` matches the old inline logic byte-for-byte
   (same `[true, false]` simple-path, same `[if, elseif[1], ...]` multi-path).
   **This is the single source of truth for two callers (If.Run, test.discover
   seeding) — any behavioural drift between old and new is a silent bug.**
4. **V1#4 OBP outside-iteration cluster** — verify the six flagged instances
   are all gone. For each new owner method added, trace callers.

### Pass 1 — OBP compliance on the *new* owner methods

The refactor added 7 new owner methods. Review each for OBP cleanliness:
- `Action.IsCondition` — pure property; no iteration. Simple.
- `Action.IsFirstConditionInStep` — delegates to `Step?.Actions.IsFirstCondition(this)`.
  Check the `?? true` default — what is the semantic when `Step == null`?
- `Actions.FirstConditionIndex` — inside iteration, owner-owned. Clean.
- `Actions.IsFirstCondition(action)` — iterates self, returns `ReferenceEquals(a, action)`.
  Check: what if `action` is not in `_items`? (Not a member of this collection.)
- `Actions.ComputeBranchChain(myIndex)` — single-source-of-truth method.
  Check edge cases: `myIndex >= Count`, `myIndex < 0`, empty `_items`.
- `Actions.SplitAtConditions(startIndex)` — check edge cases, esp. `startIndex`
  bounds and the "trailing body before first condition" case.
- `Steps.DisableChildrenOf(parent, disabled, ctx)` — check parent-not-in-collection
  case; check that the Index-based forward walk is safe after insertions/removals.
- `Goal.ForEachAction(visitor)` — straight double foreach. Check that it uses
  `_steps.Value` directly (bypassing Steps' enabled-skip iterator) — is that
  the right semantic for discover (yes, discovery walks the built graph), and
  does that contradict Steps.Run's iteration?

### Pass 2 — Simplification

Focus on the new delta:
- Does the owner-method factoring leave any residual imports, unused
  helpers, or obsolete comments in the calling files?
- Does the `int myIndex = actions.IndexOf(__action); if (myIndex < 0) myIndex = 0;`
  degrade any prior guarantee? Previously the loop fell through with `myIndex = 0`;
  the new code is semantically identical — but confirm.
- `ExtractAutoTags` and `SeedBranchChains` now collect sub-goals into a
  list and recurse after the visitor — is the visitor lambda capturing
  `subGoals` by reference? (It is — verify scoping is local to each call.)
- `seededSteps.Add(step.Index)` in `SeedBranchChains` — new deduplication
  to prevent visiting the same step twice via `ForEachAction`'s
  (step, action)-pair expansion. Confirm the old code didn't need this (it
  iterated `goal.Steps` once per step, not once per (step, action)). **This is
  a subtle new invariant introduced by the visitor pattern.**

### Pass 3 — Readability

- Are the new owner-method XML docs good?
- Is `Action.IsFirstConditionInStep` named well? (It computes a derived
  property; "first condition in step" reads fine.)
- Is `Goal.ForEachAction` named well given it takes a `(Step, Action)` visitor?
  Could be `ForEachStepAction` but the current name is acceptable.

### Pass 4 — Behavioural reasoning

This is the most important pass. The refactor introduces subtle behavioural
shifts:

1. **`Goal.ForEachAction` uses `_steps.Value`** — bypasses any iterator
   customization on `Steps`. Is that intentional? (Probably yes — discover
   walks the built graph ignoring runtime `Disabled` flags.)
2. **`SeedBranchChains` visitor pattern** — old code visited each step once,
   new code visits each (step, action) pair. The `seededSteps` HashSet
   dedupes the per-step seeding. **Verify this produces the same branchChain
   seeding as the old code for any test goal.**
3. **`IsFirstConditionInStep` fallback** — `Step?.Actions.IsFirstCondition(this)
   ?? true`. If `Step` is null, returns `true` → coverage records a branch
   label. Old code's `BranchChain.IsFirstConditionInStep` (now deleted) — what
   did it return in the same case? Check whether a null Step is actually
   reachable from the coverage subscriber in run.cs.
4. **`Actions.IsFirstCondition`** — the loop `foreach a in _items → if
   !a.IsCondition continue → return ReferenceEquals(a, action)`. This
   returns as soon as it finds the first condition. Meaning: it returns
   `true` iff `action` *is* the first condition, `false` otherwise (including
   when `action` is not in the collection at all). Semantically correct,
   but worth a sanity read.
5. **`SplitAtConditions` vs old inline code** — diff the two byte-by-byte.
   Any difference in branch-grouping produces a different set of branches
   and therefore a different branchChain.
6. **`ComputeBranchChain` vs old inline code in If.cs:159-165** — diff.
   Note the new code walks `_items` from `myIndex` and adds `"if"` then
   `"elseif[N]"` for each subsequent condition. Old inline code walked
   `branches` (post-split) and keyed off `dc == null` → `"else"`. **The new
   version does NOT emit "else"** — it can't, because it reads from
   `_items`, not from `branches`, and the "else" label was synthesized only
   at split time when `currentCondition` was null for the last body. This
   **might be a correctness regression** for future else-support, but since
   the builder never produces null-condition actions today, the chain
   emitted is identical. Flag as semantic divergence to document.
7. **Scope creep of `Actions.IsFirstCondition`** — The new `Action.IsFirstConditionInStep`
   is called from `run.cs` coverage subscriber. Any action reaching that
   subscriber has its `Step` set? Confirm.

### Pass 5 — Deletion test

For each new owner method:
- If I deleted it, would any test fail?
- Are there any methods added "for completeness" that have zero callers?
  - `Actions.FirstConditionIndex` — called from `discover.cs` SeedBranchChains. ✓
  - `Actions.IsFirstCondition` — called from `Action.IsFirstConditionInStep`. ✓
  - `Actions.ComputeBranchChain` — called from `if.cs` and `discover.cs`. ✓
  - `Actions.SplitAtConditions` — called from `if.cs`. ✓
  - `Steps.DisableChildrenOf` — called from `if.cs`. ✓
  - `Goal.ForEachAction` — called from three discover.cs methods. ✓
  - `Action.IsCondition` — called from Actions helpers + run.cs. ✓
  - `Action.IsFirstConditionInStep` — called from run.cs. ✓

## Deliverables

- `v2/result.md` — per-file findings, focused on the delta
- `v2/verdict.json` — pass or fail
- `v2/summary.md` — what I did, what I found
- `v2/changes.patch` — `git diff runtime2..HEAD -- ':(exclude).bot'`
  (should be the same as v1's patch + 8a462217's delta; analyzer makes no
  code changes)
- Update `.bot/runtime2-test-module/codeanalyzer/summary.md` cross-session
- Update `.bot/runtime2-test-module/report.json` session entry

## Open question

Since v1 findings #5 (bare catch), #6 (Cancelled status), #7-12 (smaller
simplifications), and other per-file notes (dead `CapturedOutput`, extracted
fields duplicating `Goal.X`, `Tag`-vs-lowercase naming) were **not** called
out as must-fix — the coder's commit message only mentions the four "must-fix"
items — should v2 re-flag them, or take it that the coder explicitly deferred
them? Default: re-check whether any of them were incidentally fixed by the
refactor, but do not re-open ones the coder consciously deferred.

## Going in

Based on memory: re-reviews must be thorough; quality over quantity; use
caller tracing, deletion test, OBP outside-iteration grep scan. Pass 4
(behavioural reasoning) is where drift-risk lives — spend most time there.
