# Security v1 — Plan

## Scope

Security audit of the action-modifiers feature on branch `runtime2-action-modifiers`.
Baseline: `runtime2`. 36 commits since branch. Coder has finished v4; codeanalyzer
passed v1; tester passed v4.

New attack surface introduced by the feature:

1. **`IModifier` + fold** — new dispatch path at `Action.RunAsync`.
2. **`timeout.after`** — pushes a CancellationTokenSource onto a per-context stack.
3. **`cache.wrap`** — user-controllable cache key, stores `Data` by reference.
4. **`error.handle`** — calls goals on error, mutates GoalCall parameters/state,
   retries the inner action.
5. **`timer.sleep`** — honours context cancellation.
6. **`Actions.GroupModifiers`** — builder restructuring of LLM output.
7. **Legacy removal** — `ErrorHandler.cs`, `cache/check.cs`, `cache/store.cs`,
   `error/check.cs` deleted; `Step.OnError`/`Cache`/`Timeout` removed.
   → verify nothing referenced from untrusted surfaces.

## Approach

Two phases, one pass each.

### Phase 1 — Blue team (attack surface map)
Enumerate the 6 areas above. For each: what is exposed, where the trust
boundary sits, what mitigations already exist, what is missing.

### Phase 2 — Red team (active probes)
Walk each new file line by line with these lenses:

- **Input validation** — resolved-value types from `%var%` (Data.Value): can a
  mistyped or oversized int/string cause the handler to blow up or enter a
  pathological state? `Retry` with `RetryCount = int.MaxValue` — DoS?
- **Concurrency** — `_cancellationStack` is a `System.Collections.Generic.Stack`
  (not thread-safe). Does any path see concurrent Push/Pop from parallel
  async flows on the same context? Event handlers? `context.App.RunGoalAsync`
  spawning a child that also uses timeout?
- **State mutation of shared records** — `error.handle.CallErrorGoal` writes
  `goalCall.Parameters = parameters;` and `goalCall.Action ??= ...`. If the
  GoalCall is the deserialized action record (shared across calls), this is
  a persistent cross-call mutation and a race condition.
- **Cache** — `MemoryStepCache` stores `Data` by reference; `cache.wrap`
  mutates `cached.Name = "__data__"` on hit. Is this safe when two goals hit
  the same cache key concurrently? What happens if `Data.Value` is a mutable
  user object that a later step modifies? (note: `DefaultKey` derives from
  `step.Goal.Path` — trusted; but user-supplied `Key` can collide across
  keys by design, user's prerogative.)
- **Timeout fallback** — `timeout.after` has a `catch OperationCanceledException
  when cts.IsCancellationRequested && !parentToken.IsCancellationRequested`
  fallback. Verify the when-filter can't silently swallow parent cancellation
  if parent cancelled between `await next()` returning and the when-filter
  being evaluated (TOCTOU).
- **Error chain unbounded** — `result.Error!.ErrorChain.Add(goalResult.Error!);`
  in both Goal paths. If a retry loop hits the error goal path N times…
  actually retry doesn't call CallErrorGoal inside the loop, only once. OK,
  but still worth confirming.
- **Recursion / callstack** — `CallErrorGoal` → `context.App.RunGoalAsync` →
  goal's own steps → possibly another `error.handle` → another
  `CallErrorGoal`. Is there a depth guard? What stops an error handler whose
  error goal itself errors in a cycle?
- **Builder grouping** — `GroupModifiers` iterates LLM output. A malicious or
  buggy LLM producing e.g. a non-existent module name: `IsModifier` returns
  false (type lookup miss → treated as executable) → safe. A leading
  modifier is dropped silently — confirm that's the intent and a user can
  see it (Warnings? Errors? Nothing?).
- **IgnoreError as final fallback** — order is `retry → goal → IgnoreError`.
  If retry succeeds, return. If goal succeeds, return. If both fail AND
  `IgnoreError = true`, swallow the original error. This is by design per
  the docstring. Verify it doesn't swallow cancellation or non-matching
  errors.
- **Filter defaults** — `MatchesError`: when all three filters are null,
  matches everything. Intended per docstring. Check that `StatusCode = 0`
  (default int) isn't confused with "unset" — the code uses `int?` so null
  is distinct from 0. ✓
- **GoalCall Goal.Value re-resolution** — `Goal?.Value` is accessed each
  invocation; if the underlying Data<GoalCall> resolves a fresh instance
  each time, the mutation concern reduces. Need to verify.

## Deliverables

- `.bot/runtime2-action-modifiers/security-report.json` — attack surface,
  findings (numbered, with severity, vector, fix).
- `.bot/runtime2-action-modifiers/security/v1/verdict.json` — pass/fail.
- `.bot/runtime2-action-modifiers/security/v1/summary.md` — this-version
  summary.
- `.bot/runtime2-action-modifiers/security/summary.md` — bot root pointer.
- `/learnings/runtime2-action-modifiers/security/v1/learnings.md` — anything
  new I learned from the codebase.
- Session report appended to `.bot/runtime2-action-modifiers/report.json`.
- `.bot/runtime2-action-modifiers/security/v1/changes.patch` — git diff
  (should be empty except for `.bot/` — I expect **no code edits** in this
  session, purely findings).

## Expected verdict

Based on initial read-through: the feature is clean. Codeanalyzer v1 found
1 medium (IgnoreError semantics — already redesigned since then; now
ordered `retry → goal → IgnoreError`). Most likely outcome: 1–3 low/medium
findings, overall **PASS**. I will not manufacture severity.

## Not in scope

- Behavior fixes — those go back to coder if I find anything that warrants
  it.
- Rebuilding `.pr` files — Ingi does that himself.
- Running the test suite — tester already passed.

## Open question

None blocking. Will proceed on approval.
