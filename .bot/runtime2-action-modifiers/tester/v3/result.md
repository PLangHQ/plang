# Tester v3 — Fresh-Eyes Audit of runtime2-action-modifiers Branch

**Verdict: FAIL — 5 must-fix, 4 should-fix**

Suite: 2145/2146 pass (1 pre-existing LLM snapshot failure). Coder v3's 18 new tests all pass. But fresh-eyes + coverage reveal gaps the prior rounds missed.

---

## Coverage Summary (production files on this branch)

| File | Line | Branch | Notes |
|------|------|--------|-------|
| timeout/after.cs | 100% / 86%* | 100% / 88%* | *Two class entries; catch fallback at 0% |
| cache/wrap.cs | 100% | 67%–100% | Sliding branch uncovered |
| error/handle.cs (Wrap) | 91% | 91% | Key/Message mismatch + PushError uncovered |
| error/handle.cs (Retry) | 100% | 86% | Good |
| error/handle.cs (CallErrorGoal) | 94% | 75% | Line 122 (PushError) uncovered |
| variable/set.cs | 96% | 81% | Line 33 (TryConvertTo error msg) uncovered |
| timer/sleep.cs | 50% | 100% | Run() body lines 15-16 at 0 hits |
| Modifiers/this.cs | 59%–100% | 100% | IList methods never called; RunAsync OK |
| Actions/this.cs (GroupModifiers) | 84% | 100% | Constructor/helper lines uncovered |
| Action/this.cs (WrapAround) | 81%–100% | 62%–100% | AsData lines 18-22 at 0% |

---

## MUST-FIX (5 items)

### MF-1: FALSE GREEN — `Handle_RetrySucceedsOnSecondAttempt_ReturnsSuccess`
**File:** ErrorHandleTests.cs:176
**Problem:** Test name says "ReturnsSuccess" but asserts `result.Success.IsFalse()`. The comment acknowledges it's testing persistent failure, but the name is a lie. Anyone reading test names believes the retry-success path is covered. It is NOT — `error.throw` is deterministic (always fails), so the path where attempt N succeeds after attempts 1..N-1 fail has **zero coverage**.

**Fix:** Either:
- (a) Rename to `Handle_RetryFirst_PersistentFailure_ExhaustsRetriesAndFails` (accurate name for what it does), OR
- (b) Replace with a real retry-success test using a stateful mock/counter that fails the first N calls then succeeds

Option (b) is strongly preferred — retry-success is a core behavior and needs a real test.

### MF-2: `variable.set.Run()` AsDefault path — 0% coverage
**File:** set.cs:47-51
**Problem:** The `AsDefault=true` + existing variable → early return path is never tested. This is runtime code, not just build-time validation. If someone writes `set %x% = 5, asDefault=true` and `%x%` already exists, the step should be a no-op. Nothing tests this.

**Fix:** Add 2 tests to settests.cs:
1. `Run_AsDefaultTrue_ExistingVariable_DoesNotOverwrite` — set var, then run set with AsDefault=true and different value, verify original value preserved
2. `Run_AsDefaultTrue_NoExistingVariable_SetsValue` — run set with AsDefault=true on fresh var, verify it's set

### MF-3: `timer/sleep.Run()` happy path — 0% hits on lines 15-16
**File:** sleep.cs:15-16
**Problem:** Sleep.Run() is only exercised via timeout tests where it gets cancelled before completing. The normal path (sleep completes, returns Ok) has zero coverage.

**Fix:** Add 1 test:
- `Sleep_CompletesNormally_ReturnsOk` — run timer.sleep(1) (1ms), assert result.Success is true

### MF-4: `error/handle.cs` Key and Message filter mismatch — lines 82, 84 uncovered
**File:** handle.cs:82, 84
**Problem:** Tests cover Key MATCH and Message MATCH, and StatusCode MISMATCH. But Key mismatch (filter="NotFound", error key="Timeout") and Message mismatch (filter="connection", error message="disk full") are never tested. The `return false` on these specific branches is at 0%.

**Fix:** Add 2 tests:
1. `Handle_FilterByKey_Mismatch_PropagatesError` — throw with key="Timeout", filter with key="NotFound", assert result.Success is false
2. `Handle_FilterByMessage_Mismatch_PropagatesError` — throw with message="disk full", filter with message="connection", assert result.Success is false

### MF-5: `timeout/after.cs` OperationCanceledException catch fallback — lines 47, 50-51 at 0%
**File:** after.cs:45-51
**Problem:** The catch clause for `OperationCanceledException when (cts.IsCancellationRequested && !parentToken.IsCancellationRequested)` is the fallback path for inner actions that re-throw OCE instead of returning a Data result. This path has zero coverage. The main timeout path works via the `if` check on line 39, not the catch.

**Fix:** Add 1 test that triggers the catch path. This requires an inner action that throws `OperationCanceledException` directly (not via CancellationToken.ThrowIfCancellationRequested — the timer.sleep path). Possibly use a custom action that does `throw new OperationCanceledException()` after a delay, or mock the inner delegate.

---

## SHOULD-FIX (4 items)

### SF-1: Weak assertions — goal invocation not verified
**Tests:** `Handle_GoalFirst_GoalSucceeds_ReturnsGoalResult` (line ~211), `Handle_RetryFirst_GoalSucceeds_ReturnsOk` (line ~247)
**Problem:** Both only check `result.Success is true`. Neither verifies the goal was actually called. The registered goal could be skipped entirely and the test would still pass if any other path returns success. A marker variable set by the goal would prove invocation.

### SF-2: `error/handle.cs` CallErrorGoal PushError path — line 122 uncovered
**File:** handle.cs:122
**Problem:** `callStack.PushError(action, failedResult.Error, context.Variables)` is never hit because test contexts don't have a CallStack. This is the error history recording path.

### SF-3: `error/handle.cs` combined filters never tested
**Problem:** Individual filters (StatusCode, Key, Message) are tested in isolation. No test uses 2+ filters simultaneously. MatchesError uses AND logic — a bug where filters interact incorrectly would be invisible.

### SF-4: `cache/wrap.cs` branch coverage at 67%
**Problem:** The uncovered branches are likely the sliding expiration conditional and/or null key fallback paths. The sliding test admits in a comment it can't introspect the stored CacheSettings.

---

## What's fine (coder v3 work verified)

- **Data.IsVariable**: 7 tests, all edge cases covered, property works correctly
- **Data.HasVariableReference**: 7 tests, all edge cases covered, regex works correctly  
- **variable.set.ValidateBuild**: 4 tests covering all 4 paths (literal "this", variable ref skip, type mismatch, valid match)
- **GroupModifiers**: 6 tests, excellent — covers empty, single, multiple, sorting, leading orphan, mixed
- **ModifierRegistry**: 6 tests, solid coverage of IsModifier/GetModifierOrder/Clone/Describe
- **ModifierFold**: 7 tests including the non-IModifier error path

---

## Items NOT in scope (acceptable)

- `Modifiers/this.cs` IList methods (Add, Remove, etc.) at 59% — these are thin wrappers over `List<T>`, not worth testing
- `Action.AsData()` at 0% — called in integration paths, not a risk on this branch
- `Data.@this` overall at 82% — massive file, not all paths are new on this branch
