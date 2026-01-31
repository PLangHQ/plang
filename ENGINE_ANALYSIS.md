# Engine.cs Execution Flow Analysis

## Current State

The Engine.cs file is ~1250 lines with a deep call chain for execution:

```
Run() → RunGoal() → RunSteps() → RunStep() → ProcessPrFile()
```

Each level has event handling interleaved, making the code complex and hard to follow.

## Method Analysis

### 1. Run() - Entry Point (~100 lines)
**Location:** Lines 305-450 approximately

**What it does:**
- Parses the goal name from input
- Builds setup events from EventRuntime
- Runs "before app start" events
- Runs setup goals (system goals that run before main)
- Calls `RunGoal()` for the main goal
- Runs "end app" events
- Handles KeepAlive (for long-running processes like webservers)
- Watches for file changes to rebuild

**Simplification opportunities:**
- Setup goals could be regular steps
- Event running is repetitive pattern

### 2. RunGoal() - Goal Execution (~100 lines)
**Location:** Lines 450-570 approximately

**What it does:**
- Sets context for the goal
- Runs "before goal" events
- Calls `RunSteps()`
- Runs "after goal" events
- Has overload for `GoalToCallInfo`

**Simplification opportunities:**
- Before/after goal events could be injected as steps
- The two overloads could be merged

### 3. RunSteps() - Step Iteration (~50 lines)
**Location:** Lines 570-667 approximately

**What it does:**
- Initializes goal step index
- Loops through steps starting from specified index
- Calls `RunStep()` for each step
- Handles return values and errors
- Handles `step.NextStep` for jumping
- Handles goal error events

**Simplification opportunities:**
- This is mostly a loop with error handling
- Could be merged with RunGoal()

### 4. RunStep() - Single Step Execution (~80 lines)
**Location:** Lines 688-770 approximately

**What it does:**
- Gets step from goal
- Sets context.CallingStep
- Starts stopwatch for timing
- Checks if step HasExecuted (RunOnce)
- Loads instruction from PR file
- Runs "before step" events
- Calls `ProcessPrFile()`
- Handles errors via `HandleStepError()`
- Runs "after step" events
- Resets log level

**Simplification opportunities:**
- Before/after step events could be injected as steps
- Error handling could be extracted

### 5. ProcessPrFile() - Module Execution (~100 lines)
**Location:** Lines 876-983 approximately

**What it does:**
- Checks if step is disabled
- Gets module type from TypeHelper
- Loads instruction if not loaded
- Adds goal/step/instruction to context
- Creates module instance via DI
- Handles MissingSettingsException
- Initializes module instance
- Sets up cancellation timeout
- Calls `classInstance.Run()`
- Handles timeout/cancellation

**Simplification opportunities:**
- This is mostly module instantiation and execution
- Could be simplified but is core functionality

### 6. HandleStepError() - Error Handling (~70 lines)
**Location:** Lines 774-841 approximately

**What it does:**
- Returns if error is already handled
- Special case for ThrowErrorModule
- Handles `AskUserError` - prompts user and retries
- Handles `FileAccessRequestError` - requests access and retries
- Finds error handler for step
- Checks retry limits
- Runs retry with delay
- Runs "on error" step events
- Handles step.Retry flag

**Simplification opportunities:**
- AskUser and FileAccess handling could be separate modules
- Error handling could be plang code

## Event Handling Analysis

Events are called at multiple levels:
1. **App level:** "before app start", "end app"
2. **Goal level:** "before goal", "after goal", "goal error"
3. **Step level:** "before step", "after step", "on error step"

This creates 7 event hook points, each with its own handling code.

## Proposed Flatter Architecture

### Option 1: Events as Steps (User's Preference)

Convert events to regular steps that get injected into the goal when loaded:

```csharp
// When goal is loaded, inject event steps:
Goal LoadGoal(string goalName) {
    var goal = prParser.ParseGoalFile(goalName);

    // Inject before-goal event steps at start
    var beforeEvents = eventRuntime.GetEventsForGoal(goal, EventType.Before);
    foreach (var eventStep in beforeEvents) {
        goal.GoalSteps.Insert(0, eventStep);
    }

    // Inject after-goal event steps at end
    var afterEvents = eventRuntime.GetEventsForGoal(goal, EventType.After);
    foreach (var eventStep in afterEvents) {
        goal.GoalSteps.Add(eventStep);
    }

    return goal;
}
```

Then execution becomes much simpler:

```csharp
public async Task<(object?, IError?)> RunGoal(Goal goal, PLangContext context) {
    foreach (var step in goal.GoalSteps) {
        var result = await ExecuteStep(goal, step, context);
        if (result.Error != null) {
            // Return/exit is signaled via EndGoal error type
            if (result.Error is EndGoal) return result;
            return result;
        }
    }
    return (null, null);
}

private async Task<(object?, IError?)> ExecuteStep(Goal goal, GoalStep step, PLangContext context) {
    // Note: step is readonly/shared - use context for mutable state
    if (!step.Execute) return (null, null);

    LoadInstruction(goal, step);

    var classType = typeHelper.GetRuntimeType(step.ModuleType);
    var instance = container.GetInstance(classType) as BaseProgram;
    instance.Init(container, goal, step, step.Instruction, contextAccessor);

    return await instance.Run();
}
```

### Option 2: Simplified Event Handler

Keep events separate but simplify the pattern:

```csharp
public async Task<(object?, IError?)> RunGoal(Goal goal, PLangContext context) {
    // Single event call pattern
    var error = await RunEvents(EventType.BeforeGoal, goal);
    if (error != null) return (null, error);

    foreach (var step in goal.GoalSteps) {
        error = await RunEvents(EventType.BeforeStep, goal, step);
        if (error != null) return (null, error);

        var result = await ExecuteStep(goal, step, context);

        if (result.Error != null) {
            error = await RunEvents(EventType.OnError, goal, step, result.Error);
            if (error != null) return (null, error);
        }

        error = await RunEvents(EventType.AfterStep, goal, step);
        if (error != null) return (null, error);

        if (step.IsReturn) return result;
    }

    error = await RunEvents(EventType.AfterGoal, goal);
    return (null, error);
}
```

## Recommended Approach

Based on user's preference for "events should just be steps":

1. **Phase 2a:** Modify goal loading to inject event steps
2. **Phase 2b:** Flatten RunGoal/RunSteps into single method
3. **Phase 2c:** Move error handling to plang where possible
4. **Phase 2d:** Remove redundant event handling code

## Files to Modify

1. `PLang/Runtime/Engine.cs` - Main execution flow
2. `PLang/Events/EventRuntime.cs` - Convert events to steps
3. `PLang/Building/Parsers/PrParser.cs` - Inject events when loading goals
4. `PLang/Building/Model/Goal.cs` - May need event step markers

## Estimated Line Reduction

- Current Engine.cs: ~1250 lines
- After flattening: ~400-500 lines (60% reduction)
- Event handling consolidated
- Error handling simplified

## Implementation Progress

### Phase 2a: Flat Execution Model (DONE)

Added `ExecuteGoalFlat()` method to Engine.cs that combines the old RunGoal + RunSteps + RunStep chain into a single method.

**Location:** `PLang/Runtime/Engine.cs` - Line 1251+

**Key changes:**
- Single method handles goal entry, step iteration, and exit
- Events called at consistent points (before/after goal, before/after step)
- Error handling integrated into the step loop
- Reduced call depth from 4 levels to 1

**Interface updated:**
- Added `ExecuteGoalFlat()` to `IEngine` interface

**Next steps:**
1. ~~Update callers to use `ExecuteGoalFlat` instead of `RunGoal`~~ (DONE)
2. Remove old `RunGoal`, `RunSteps`, `RunStep` methods once migration complete
3. ~~Consider extracting event calls into a helper for even cleaner code~~ (DONE - IEventProvider)

### Phase 2b: Events as Steps (DONE)

Added `IEventProvider` interface to EventRuntime with methods to get and execute events as if they were steps.

**Location:** `PLang/Events/EventRuntime.cs`

**New methods on IEventRuntime:**
- `GetBeforeGoalEvents(goal)` - Get events to run before goal
- `GetAfterGoalEvents(goal)` - Get events to run after goal
- `GetBeforeStepEvents(goal, step)` - Get events to run before step
- `GetAfterStepEvents(goal, step)` - Get events to run after step
- `ExecuteEvent(evt, sourceGoal, sourceStep)` - Execute a single event like a step

**Updated ExecuteGoalFlat:**
- Now uses `GetBeforeGoalEvents/GetAfterGoalEvents` instead of `RunGoalEvents`
- Now uses `GetBeforeStepEvents/GetAfterStepEvents` instead of `RunStepEvents`
- Events are executed inline via `ExecuteEvent()` making them conceptually "steps"

**Benefits:**
- Events are now treated uniformly as executable items
- The execution loop is more explicit about what runs when
- Easier to understand the execution flow
- Maintains thread safety (no modification of shared goal objects)

### Phase 2c: Simplified Error Handling (DONE)

Added `HandleStepErrorFlat()` method that provides cleaner error handling for the flat execution model.

**Location:** `PLang/Runtime/Engine.cs`

**Key changes:**
- New `HandleStepErrorFlat()` returns `(bool ShouldRetry, IError? Error)` for cleaner control flow
- Retry loop is now explicit in `ExecuteGoalFlat` instead of recursive calls
- Before/after step events run once even during retries
- Clear separation between: AskUser handling, FileAccess handling, step error handlers, and error events

**Error handling flow:**
```
1. Check if error is already handled → return
2. Check if it's a ThrowError → propagate
3. Handle AskUser errors → prompt, retry
4. Handle FileAccess errors → request permission, retry
5. Check step error handlers → retry if configured
6. Run step error events → may mark for retry
7. Check post-event retry flag → retry if set
8. Return final error or null
```

**Benefits:**
- Error handling is no longer recursive (was calling RunStep → HandleStepError → RunStep)
- Retry logic is explicit and easier to follow
- Before/after step events don't run multiple times during retries
- Cleaner separation of concerns
