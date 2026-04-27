# Tester v1 — Action Modifiers Branch

## Test Suite Summary

| Suite | Tests | Pass | Fail |
|-------|-------|------|------|
| ErrorHandleTests | 10 | 10 | 0 |
| ModifierFoldTests | 7 | 7 | 0 |
| TimeoutAfterTests | 6 | 6 | 0 |
| CacheWrapTests | 8 | 8 | 0 |
| ModifierRegistryTests | 6 | 6 | 0 |
| GroupModifiersTests | 6 | 6 | 0 |
| **Full C# suite** | **2122** | **2121** | **1** |

1 failure is pre-existing (LLM snapshot test `Query_ToolCall_LlmRequestsToolAndHandlesError`), unrelated to modifiers.

---

## Coverage — Modifier Production Files

| File | Line% | Uncovered |
|------|-------|-----------|
| `error/handle.cs` | ~65% | L42-47, L56-61, L82, L84, L108-129 |
| `timeout/after.cs` | 93% | L47-51 (OCE fallback catch) |
| `cache/wrap.cs` | 100% | — |
| `Modifiers/this.cs` | 75% | L23-32 (IList boilerplate: AddRange/Clear/Contains/etc.) |
| `ModifierAttribute.cs` | 100% | — |
| `IModifier.cs` | N/A | Interface only |

---

## CRITICAL: Latest Fix at 0% Coverage

Commit `f3752384` ("Fix error.handle: propagate chained errors when error goal fails + avoid parameter mutation") is **completely untested**. Specifically:

### 1. `CallErrorGoal` method (L106-129) — 0% coverage
The entire method has never been called by any C# test. No test configures a `Goal` parameter on error.handle. This means:
- Error goal invocation is untested
- `!error` parameter injection is untested
- CallStack error recording is untested
- Action stamping on GoalCall is untested

### 2. GoalFirst path (L39-49) — goal branch at 0%
Lines 42-47: `CallErrorGoal` → success check → `ErrorChain.Add` on failure. This is the core of the fix — **goal failure now chains instead of silently succeeding** — and it has zero coverage.

### 3. RetryFirst path (L53-61) — goal branch at 0%
Lines 56-61: Same pattern. The old code did `return Ok()` unconditionally after calling the goal. The fix now checks `goalResult.Success` before returning Ok, and chains the error if the goal fails. Zero coverage.

### 4. Parameter mutation fix (L109-112) — 0% coverage
The fix changed from mutating `goalCall.Parameters` directly (via `.RemoveAll()` + `.Add()`) to creating a new list via LINQ. This avoids corrupting the original GoalCall if it's called twice (e.g., RetryFirst with goal). Never exercised.

---

## False Green Analysis

### F1. `Handle_RetryFirst_RetriesBeforeCallingGoal` — name overpromises
Test name says "retries before calling goal" but **no goal is configured**. It only tests retry exhaustion → failure. Doesn't verify ordering at all. Should be renamed to `Handle_RetryFirst_NoGoal_ExhaustsRetriesAndFails`.

### F2. `Handle_GoalFirst_CallsGoalBeforeRetry` — name overpromises
Same issue. No goal configured. Only verifies no-goal + retry = failure. Doesn't verify goal-first ordering. Should be renamed to `Handle_GoalFirst_NoGoal_ExhaustsRetriesAndFails`.

### F3. `Handle_RetrySucceedsOnSecondAttempt_ReturnsSuccess` — deferred coverage claim
Comment says "covered end-to-end by OnErrorRetry.test.goal in the PLang suite." But:
- PLang test runner has a known assertion-swallowing bug
- PLang tests not yet verified working on this branch
- The C# test only verifies persistent failure, not retry success

### F4. Deletion test on MatchesError Key/Message filters
`Handle_FilterByKey_CaseInsensitiveMatch` and `Handle_FilterByMessage_SubstringMatch` both test the match path but coverage shows L82/L84 (the actual string comparison) are partially uncovered. The non-matching side of these filters may not be fully exercised.

---

## Uncovered timeout/after.cs Path (Low Priority)

Lines 47-51: OCE fallback catch. Only triggers when an inner handler re-throws `OperationCanceledException` directly instead of wrapping it into a Data result. Tests cover the normal timeout path (408 via `cts.IsCancellationRequested && !result.Success`) but not this catch block. Low risk — defensive path.

---

## Uncovered Modifiers/this.cs (Low Priority)

Lines 23-32: IList<PrAction> boilerplate methods (AddRange, Clear, Contains, CopyTo, IndexOf, Insert, Remove, RemoveAt). These are trivial List<T> delegations. Not worth dedicated tests.

---

## Verdict: FAIL

The latest fix (commit `f3752384`) introduced behavioral changes to error.handle that are at **0% coverage**. The entire `CallErrorGoal` method, the GoalFirst error-chaining path, and the RetryFirst goal-success-check path have never been called by any C# test.

### Required for coder (must-fix before PASS):

1. **Test: GoalFirst + goal succeeds → returns goal result** (verifies L43-44)
2. **Test: GoalFirst + goal fails → error propagates with chained error** (verifies L44-47)
3. **Test: RetryFirst + goal succeeds → returns Ok** (verifies L57-58)
4. **Test: RetryFirst + goal fails → error propagates with chained error** (verifies L58-61)
5. **Test: CallErrorGoal injects !error parameter** (verifies L109-112)
6. **Test: GoalCall.Parameters not mutated after CallErrorGoal** (verifies the parameter mutation fix)
7. **Rename F1/F2 tests** to match what they actually verify

### Nice-to-have:

8. Test: timeout/after OCE fallback catch path (L47-51)
9. Test: retry-success-on-second-attempt with a stateful mock (not deferred to PLang suite)
