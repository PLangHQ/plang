# Tester v2 — Action Modifiers Branch (Fresh-Eye Pass)

## Test Suite Summary

| Suite | Tests | Pass | Fail |
|-------|-------|------|------|
| ErrorHandleTests | 16 | 16 | 0 |
| ModifierFoldTests | 7 | 7 | 0 |
| TimeoutAfterTests | 6 | 6 | 0 |
| CacheWrapTests | 8 | 8 | 0 |
| ModifierRegistryTests | 6 | 6 | 0 |
| GroupModifiersTests | 6 | 6 | 0 |
| **Full C# suite** | **2128** | **2127** | **1** |

1 failure is pre-existing (LLM snapshot test `Query_ToolCall_LlmRequestsToolAndHandlesError`), unrelated.

No `NotImplementedException` stubs found in any test file.

---

## v1 Must-Fix Items — Status

All 7 must-fix items from tester v1 are addressed by coder v2:

| # | Item | Status |
|---|------|--------|
| 1 | GoalFirst + goal succeeds | COVERED: `Handle_GoalFirst_GoalSucceeds_ReturnsGoalResult` |
| 2 | GoalFirst + goal fails → error chains | COVERED: `Handle_GoalFirst_GoalFails_ErrorChains` |
| 3 | RetryFirst + goal succeeds | COVERED: `Handle_RetryFirst_GoalSucceeds_ReturnsOk` |
| 4 | RetryFirst + goal fails → error chains | COVERED: `Handle_RetryFirst_GoalFails_ErrorChains` |
| 5 | !error parameter injection | COVERED: `Handle_CallErrorGoal_InjectsErrorParameter` |
| 6 | Parameter mutation avoidance | COVERED: `Handle_CallErrorGoal_DoesNotMutateOriginalParameters` |
| 7 | Rename F1/F2 tests | DONE: renamed to `_NoGoal_ExhaustsRetriesAndFails` |

---

## Coverage — Modifier Production Files (v2)

| File | v1 | v2 | Delta | Remaining uncovered |
|------|----|----|-------|---------------------|
| `error/handle.cs` | 65% | **96%** | +31% | L82, L84 (filter non-match), L122 (PushError) |
| `timeout/after.cs` | 93% | 93% | — | L47-51 (OCE fallback catch) |
| `cache/wrap.cs` | 100% | 100% | — | — |
| `Modifiers/this.cs` | 75% | 75% | — | L23-32 (IList boilerplate) |
| `ModifierAttribute.cs` | 100% | 100% | — | — |
| `Actions/this.cs` (GroupModifiers) | 86% | 86% | — | L18,27,29-32,40 |
| `timer/sleep.cs` | 50% | 50% | — | L16-17 (Run method) |

---

## Fresh-Eye Findings (Beyond v1)

### F1. `Data.IsVariable` and `Data.HasVariableReference` — 0% test coverage (MEDIUM)

Two new properties added in commit `ad03aead`:
- `IsVariable`: checks if value is exactly `%var%` pattern
- `HasVariableReference`: regex check for any `%var%` in string

These are used in `variable.set.ValidateBuild()` to skip build-time validation on runtime-resolved values. Neither has any test. The regex `%[^%]+%` could silently mismatch edge cases (nested `%%`, empty `%%`, Unicode).

**Impact**: Build-time validation could produce false positives/negatives without detection.

### F2. `variable.set.ValidateBuild()` — 0% test coverage (MEDIUM)

New build-time validation method (commit `ad03aead`) with 3 distinct paths:
1. Literal "this" detection → returns error string
2. `HasVariableReference` → skips validation (returns null)
3. Type mismatch → attempts TryConvertTo → returns error on failure

Zero references in test files. This is the only `IBuildValidatable` implementation on the branch.

**Impact**: Builder could accept invalid steps or reject valid ones without detection.

### F3. `error/handle.cs` L82, L84 — filter non-match paths (LOW)

`MatchesError` Key and Message filter return-false paths are uncovered. Tests cover matching keys/messages but not the case where Key is set but doesn't match the error's Key, or Message is set but error.Message doesn't contain it.

### F4. `error/handle.cs` L122 — CallStack.PushError (LOW)

`callStack.PushError(action, failedResult.Error, context.Variables)` is never called because tests don't have a Step with Actions set on the context. The callstack recording during error goal calls is untested.

### F5. `timer/sleep.cs` Run() — 50% coverage (LOW)

Lines 16-17 (`Task.Delay` + return Ok) uncovered. Trivial, but `sleep` is the new action added specifically for this branch's PLang timeout tests.

### F6. `timeout/after.cs` L47-51 — OCE fallback catch (LOW)

The catch-and-re-throw path for inner handlers that throw `OperationCanceledException` directly instead of wrapping it. Defensive code, hard to trigger in unit tests.

---

## Assertion Quality — Coder v2 Tests

The 6 new goal-path tests are well-structured. Specific checks:

- `Handle_GoalFirst_GoalFails_ErrorChains`: Asserts `ErrorChain.Count > 0` AND `ErrorChain[0].Message == "goal failed"`. Strong — verifies both chaining and message propagation.
- `Handle_RetryFirst_GoalFails_ErrorChains`: Same pattern. Strong.
- `Handle_CallErrorGoal_InjectsErrorParameter`: Checks `Ctx.Variables.GetValue("!error")` is not null. Moderate — verifies injection happened but not what was injected.
- `Handle_CallErrorGoal_DoesNotMutateOriginalParameters`: Checks original list count and first element name. Strong — directly verifies the fix.
- `Handle_GoalFirst_GoalSucceeds_ReturnsGoalResult`: Only checks `result.Success`. Moderate — doesn't verify the goal actually ran (e.g., check if `%marker%` was set).
- `Handle_RetryFirst_GoalSucceeds_ReturnsOk`: Only checks `result.Success`. Same note.

---

## Verdict: FAIL

The v1 critical gap (error.handle goal paths at 0%) is fixed — coverage jumped from 65% to 96%. All 7 must-fix items are addressed. No stub tests found.

But fresh-eye pass found more new code at 0% coverage. Same standard as v1: new code needs tests.

### Must-fix for coder:

1. **Test `Data.IsVariable`** — edge cases: `%var%` (true), `%v%` (true), `%%` (false), `hello %var%` (false), `%var% + 1` (false), non-string value (false)
2. **Test `Data.HasVariableReference`** — edge cases: `hello %name%` (true), `%a% + %b%` (true), `no vars` (false), `%%` (false), non-string value (false)
3. **Test `variable.set.ValidateBuild()`** — 3 paths:
   - Literal "this" as Value → returns error string
   - Value with `%variable%` reference → returns null (skips validation)
   - Type mismatch (e.g., type=int, value="not a number") → returns error string
   - Valid type match → returns null

### Nice-to-have (won't block):

- F3: MatchesError non-match assertions
- F4: CallStack.PushError in error goal path
- Strengthen `GoalSucceeds` tests to verify side effects (check `%marker%` was set)
