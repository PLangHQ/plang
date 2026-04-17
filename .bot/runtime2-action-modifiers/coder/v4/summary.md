# Coder v4 Summary — Tester v3 Must-Fix Items

## What this is
Tester v3 did a fresh-eyes audit and found 5 must-fix items: a false-green retry test, missing filter mismatch coverage, sleep happy path at 0%, and the OCE catch fallback in timeout/after uncovered. Also flagged AsDefault at 0% (tests already existed from v3).

## What was done
5 new tests added, 1 test renamed. All test-only changes — no production code modified.

### MF-1: False-green retry test
- Renamed `Handle_RetrySucceedsOnSecondAttempt_ReturnsSuccess` → `Handle_RetryFirst_PersistentFailure_AllRetriesFail` (accurate name)
- Added real `Handle_RetrySucceedsOnSecondAttempt_ReturnsSuccess` using `Modifiers.RunAsync()` with a stateful lambda that fails first call then succeeds. Verifies retry-success path and that `callCount == 2`.

### MF-4: Key/Message filter mismatch
- `Handle_FilterByKey_Mismatch_PropagatesError` — throw key="Timeout", filter key="NotFound" → error propagates
- `Handle_FilterByMessage_Mismatch_PropagatesError` — throw message="disk full", filter message="connection" → error propagates

### MF-3: Sleep happy path
- New `PLang.Tests/App/Modules/timer/SleepTests.cs` with `Sleep_CompletesNormally_ReturnsOk`

### MF-5: OCE catch fallback
- `After_InnerThrowsOCE_CatchFallbackReturnsTimeoutError` — uses `Modifiers.RunAsync()` with a throwing inner lambda and 1ms timeout. Triggers the `catch(OperationCanceledException)` path in after.cs:45-51.

### MF-2: AsDefault tests
- Already existed from coder v3 (`Set_AsDefault_DoesNotOverwriteExisting`, `Set_AsDefault_SetsWhenNotExists`). Both pass. Tester coverage may have been stale.

## Files modified
- `PLang.Tests/App/Modules/modifier/ErrorHandleTests.cs` — renamed 1, added 3 tests
- `PLang.Tests/App/Modules/modifier/TimeoutAfterTests.cs` — added 1 test
- `PLang.Tests/App/Modules/timer/SleepTests.cs` — new file, 1 test

## Code example
The stateful retry-success test pattern (MF-1):
```csharp
int callCount = 0;
Func<Task<Data>> statefulNext = () =>
{
    callCount++;
    if (callCount == 1)
        return Task.FromResult(Data.FromError(new ServiceError("transient", "TransientError", 503)));
    return Task.FromResult(Data.Ok());
};
var modifiers = new ActionModifiers { ErrorHandler(("retryCount", 3)) };
var result = await modifiers.RunAsync(statefulNext, Ctx);
// result.Success is true, callCount == 2
```

## Test results
2150/2151 pass (1 pre-existing LLM snapshot failure, unrelated).
