# Coder v4 Plan — Tester v3 Must-Fix Items

## 5 must-fix items, all test-only changes

### MF-1: Fix false-green retry test + add real retry-success test
**File:** `PLang.Tests/App/Modules/modifier/ErrorHandleTests.cs`

- Rename `Handle_RetrySucceedsOnSecondAttempt_ReturnsSuccess` → `Handle_RetryFirst_PersistentFailure_AllRetriesFail`
- Add a real retry-success test using a stateful `Func<Task<Data>>` that fails the first call then succeeds. This requires creating the modifier fold manually (can't use error.throw since it always fails). Will build a PrAction with a custom inner delegate via the Modifiers.RunAsync path, or use a variable.set action that succeeds on retry by pre-setting the variable on second call.

**Approach:** The simplest way is to test at the `Wrap()` level directly — call `handle.Wrap(statefulNext, context)` where `statefulNext` is a lambda that fails once then succeeds. This isolates the retry logic without needing a custom module.

### MF-2: Verify AsDefault tests actually hit Run()
**File:** `PLang.Tests/App/Modules/variable/settests.cs`

Tests `Set_AsDefault_DoesNotOverwriteExisting` and `Set_AsDefault_SetsWhenNotExists` already exist. The tester says 0% coverage on lines 47-51. Need to verify these tests actually execute the `AsDefault.Value == true` branch. The parameter name is `"asdefault"` (lowercase) — need to confirm the source generator resolves this case-insensitively.

**Action:** Run the tests with debug output to verify they hit the AsDefault path. If they don't (parameter name mismatch), fix the parameter name.

### MF-3: `timer/sleep.Run()` happy path
**File:** New file `PLang.Tests/App/Modules/timer/SleepTests.cs`

Add 1 test: `Sleep_CompletesNormally_ReturnsOk` — run timer.sleep(1) via `_app.Run()`, assert success.

### MF-4: error/handle Key/Message filter mismatch
**File:** `PLang.Tests/App/Modules/modifier/ErrorHandleTests.cs`

Add 2 tests:
1. `Handle_FilterByKey_Mismatch_PropagatesError` — throw with key="Timeout", filter key="NotFound"
2. `Handle_FilterByMessage_Mismatch_PropagatesError` — throw with message="disk full", filter message="connection"

### MF-5: timeout/after OCE catch fallback
**File:** `PLang.Tests/App/Modules/modifier/TimeoutAfterTests.cs`

Add 1 test that triggers the `catch (OperationCanceledException)` path. The catch fires when an inner action throws OCE directly (instead of returning a Data result with error). Need to test at the `Wrap()` level — call `after.Wrap(throwingNext, context)` where `throwingNext` throws `new OperationCanceledException()` after a short delay while the timeout CTS is cancelled.

**Approach:** Use a very short timeout (1ms) with an inner func that does `await Task.Delay(100)` then `throw new OperationCanceledException()`. The timeout CTS will fire first, and when the inner task throws OCE, the catch clause matches because `cts.IsCancellationRequested && !parentToken.IsCancellationRequested`.

## Order of work
1. MF-4 (trivial additions to ErrorHandleTests)
2. MF-1 (rename + new retry test in ErrorHandleTests)  
3. MF-3 (new SleepTests.cs)
4. MF-2 (verify/fix AsDefault tests)
5. MF-5 (OCE catch path test)
6. Run full suite, verify all pass
