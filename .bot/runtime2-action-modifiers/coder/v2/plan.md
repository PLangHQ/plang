# Coder v2 Plan — Error Handle Goal-Path Tests

## Goal

Add 7 tests to cover the error.handle `CallErrorGoal` path and rename 2 misleading tests, as required by tester's FAIL verdict.

## Approach

The tests need a real goal that `RunGoalAsync` can execute. Strategy:
- Create a simple in-memory Goal with a `variable.set` step (always succeeds) or `error.throw` step (always fails)
- Register it in `app.Goals.Add()` so `GoalCall.GetGoalAsync` finds it via `app.Goals.Get(Name)`
- The error.handle modifier's `CallErrorGoal` then invokes it through the real pipeline

### Helper Method

Add to `ErrorHandleTests`:
```csharp
private Goal CreateGoal(string name, PrAction action)
{
    var step = new Step { Text = "test step" };
    step.Actions.Add(new Action { Module = action.Module, ActionName = action.ActionName, Parameters = action.Parameters });
    var goal = new Goal { Name = name, Path = $"/{name}.goal" };
    goal.Steps.Add(step);
    return goal;
}
```

Two goal types:
- **SuccessGoal**: `variable.set` with `name=%marker%`, `value=handled` — always succeeds, verifiable via memory
- **FailGoal**: `error.throw` with `message=goal failed` — always fails with an error

### Test 1: GoalFirst + goal succeeds → returns goal result (L43-44)

Configure: `error.throw "boom"` + error.handle with `goal=GoalCall("SuccessGoal")`, `order=GoalFirst`.
Assert: `result.Success == true` (goal succeeded, returned goalResult).

### Test 2: GoalFirst + goal fails → error chains (L44-47)

Configure: `error.throw "boom"` + error.handle with `goal=GoalCall("FailGoal")`, `order=GoalFirst`.
Assert: `result.Success == false`, `result.Error.ErrorChain.Count > 0`, chain contains the goal's error.

### Test 3: RetryFirst + goal succeeds → returns Ok (L57-58)

Configure: `error.throw "persistent"` + error.handle with `goal=GoalCall("SuccessGoal")`, `order=RetryFirst`.
Assert: `result.Success == true`.

### Test 4: RetryFirst + goal fails → error chains (L58-61)

Configure: `error.throw "persistent"` + error.handle with `goal=GoalCall("FailGoal")`, `order=RetryFirst`.
Assert: `result.Success == false`, `result.Error.ErrorChain.Count > 0`.

### Test 5: CallErrorGoal injects !error parameter (L109-112)

Configure: `error.throw "injected"` + error.handle with `goal=GoalCall("SuccessGoal")`, `order=GoalFirst`.
After run: check `Ctx.Variables.GetValue("!error")` is the original error.

### Test 6: GoalCall.Parameters not mutated (L109-112)

Create a GoalCall with initial parameters `[("extra", "val")]`. Run through handle.
Assert: original parameters list still has only 1 item (not polluted with `!error`).

### Test 7: Rename F1/F2

- `Handle_RetryFirst_RetriesBeforeCallingGoal` → `Handle_RetryFirst_NoGoal_ExhaustsRetriesAndFails`
- `Handle_GoalFirst_CallsGoalBeforeRetry` → `Handle_GoalFirst_NoGoal_ExhaustsRetriesAndFails`

## Files Modified

- `PLang.Tests/App/Modules/modifier/ErrorHandleTests.cs` — add 6 new tests, rename 2 existing, add helper

## Verification

- `dotnet run --project PLang.Tests` — all tests pass
