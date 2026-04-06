# Tester v3 Findings for Coder v4

Tester verdict: PASS — but 3 gaps to fix (F3 skipped, not ready yet).

## F1 (Major): Steps.RunAsync discards Record() return value

**File**: `PLang/App/Engine/Goals/Goal/Steps/this.cs`, line 47

```csharp
await context.Setup.Record(step, engine, stepResult.Success ? null : stepResult.Error);
```

The return value (`Data`) is discarded. Code analyzer v1 flagged `Record` as swallowing errors. Coder v2 changed the return type to `Task<Data>`, but the **caller** still ignores it.

**Fix needed**: Either:
- (A) If discarding is intentional (re-run is safe), add a comment explaining the decision. Add a test that verifies setup completes successfully even when `Record` fails (mock/corrupt the DataSource).
- (B) If recording failure should abort setup, check the return value and propagate the error.

Pick whichever matches the design intent. Either way, add a test.

## F2 (Minor): Skip test doesn't prove step was skipped

**File**: `PLang.Tests/App/Goals/Setup/SetupTests.cs` — `RunAsync_SkipsAlreadyExecutedSteps`

The test pre-records step1, runs setup, then checks both steps are recorded. But re-executing step1 and re-recording it would produce the same result. The "skip" at line 36-37 of Steps.RunAsync has no direct verification.

**Fix needed**: Add a side-effect that proves step1 did NOT execute. Options:
- Use a variable.set action in step1's actions and verify the variable was NOT set after RunAsync
- Or add a counter mechanism
- Or check that the DataSource entry for step1 was not modified (same timestamp/data)

## F4 (Minor): Cancellation check untested

**File**: `PLang/App/Engine/Goals/Goal/Steps/this.cs`, line 56-57

```csharp
if (cancellationToken.IsCancellationRequested)
    return Data.FromError(GoalError.Cancelled(context));
```

**Fix needed**: Add a test that passes a pre-cancelled (or mid-execution cancelled) token to `Steps.RunAsync` and verifies it returns `GoalError.Cancelled`.
