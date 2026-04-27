# Coder v2 Summary — Error Handle Goal-Path Tests

## What this is

Tests for the error.handle `CallErrorGoal` code path introduced in commit f3752384. The tester found this entire path was at 0% coverage — the fix that propagates chained errors when error goals fail, and avoids parameter mutation, had no tests exercising it.

## What was done

Added 6 new tests and renamed 2 existing ones in `PLang.Tests/App/Modules/modifier/ErrorHandleTests.cs`:

**New tests:**
1. `Handle_GoalFirst_GoalSucceeds_ReturnsGoalResult` — verifies L43-44
2. `Handle_GoalFirst_GoalFails_ErrorChains` — verifies L44-47 (error chaining)
3. `Handle_RetryFirst_GoalSucceeds_ReturnsOk` — verifies L57-58
4. `Handle_RetryFirst_GoalFails_ErrorChains` — verifies L58-61 (error chaining)
5. `Handle_CallErrorGoal_InjectsErrorParameter` — verifies L109-112 (!error injection)
6. `Handle_CallErrorGoal_DoesNotMutateOriginalParameters` — verifies LINQ-based new list

**Renames:**
- `Handle_RetryFirst_RetriesBeforeCallingGoal` → `Handle_RetryFirst_NoGoal_ExhaustsRetriesAndFails`
- `Handle_GoalFirst_CallsGoalBeforeRetry` → `Handle_GoalFirst_NoGoal_ExhaustsRetriesAndFails`

**Helper added:** `RegisterGoal()` — creates an in-memory Goal with a single action step and registers it in `app.Goals` so `GoalCall.GetGoalAsync` can find it via `app.Goals.Get(Name)`.

## Code example

```csharp
[Test]
public async Task Handle_GoalFirst_GoalFails_ErrorChains()
{
    RegisterGoal("FailGoal", "error", "throw", ("message", "goal failed"));
    var goalCall = new GoalCall { Name = "FailGoal" };
    var action = Throw("original error",
        modifiers: new ActionModifiers
        {
            ErrorHandler(("goal", goalCall), ("order", "GoalFirst"))
        });

    var result = await action.RunAsync(Ctx);

    await Assert.That(result.Success).IsFalse();
    await Assert.That(result.Error!.ErrorChain.Count).IsGreaterThan(0);
    await Assert.That(result.Error.ErrorChain[0].Message).IsEqualTo("goal failed");
}
```

## Results

- ErrorHandleTests: 16/16 pass
- Full suite: 2127/2128 (1 pre-existing LLM snapshot failure)
