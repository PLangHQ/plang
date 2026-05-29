# Tester summary — builder-ergonomics

**Version:** v2
**Verdict:** PASS

## What this is

The `builder-ergonomics` branch worked through a 7-priority friction list
(`user-feedback.md`). Shipped: a per-channel `IsExecuting` recursion guard (replacing the
foundational-snapshot mechanism), P4 root-cause-first error chaining, builder output routed
through a named `"builder"` goal-channel, and confidence-per-step (P6) in the LLM passes.

## History

- **v1 (FAIL):** 1 critical false-green (`UnknownVerb.test.goal` asserted nothing and shipped a
  `.pr` that mis-compiled `compress`→`variable.set`), 1 flaky C# test (cache count under
  parallel run), 1 mutation-confirmed untested channel guard, + 2 minor (env 502, prompt leak).
- **v2 (PASS):** coder addressed all three actionable findings; re-tested and independently
  verified.

## What was done (v2 re-test)

Clean rebuild (stale-binary protocol). Both suites green:
- **C#:** 3377/3377 — 0 fail (v1 cache flake gone).
- **PLang:** 234/234 — 0 fail, 0 timeout.

### Findings resolved

- **F1 — false green deleted.** `Tests/ConfidenceCheck/` removed. Correct call (a real
  trace-based assert would be flaky or fixture-bound). *Residual:* P6 now has no automated
  test — observation, not a blocker.
- **F2 — cache test fixed.** Now asserts `ReferenceEquals(PropertiesFor(t,Out),
  PropertiesFor(t,Out))` — per-key identity, no global counter, no parallel race. Global
  `ClearCacheForTests`/`CacheSize` API removed.
- **F3 — channel guard covered end-to-end.** New `Tests/Channels/GoalChannelRecursion/` test:
  a goal-channel writes its own name, asserts `errorKey == 'ChannelNotFound'`. `.pr` actions
  match step text (no builder false-green).

### Mutation re-verification (the key check)

Independently re-ran the v1 mutation — deleted `_executing.Value = true` in `InvokeGoal`,
rebuilt, re-ran the recursion test:

```
[Fail] /Channels/GoalChannelRecursion/Start.test.goal
  Expected: "ChannelNotFound"
  Actual:   "CallStackOverflow"
```

Without the guard, the self-write recurses to the engine's call-stack backstop; the test fails
because it asserts the *specific* `ChannelNotFound` key (a weaker "any error" assert would pass
on the backstop). Reverted, rebuilt, re-ran → 234/234 green. The guard-arming path that was
invisible to all 7 v1 reflection-flip tests is now genuinely covered.

## Deferred (accepted, separate tickets)

- **F4** `UploadFile` 502 — environmental Http; all Http tests passed this run.
- **F5** `y/n/a` permission-prompt leak — fixture hygiene.

## Next

Branch is green and honestly tested. Ready for security review.
