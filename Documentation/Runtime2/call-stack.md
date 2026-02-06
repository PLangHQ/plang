# CallStack & Debugging

Optional execution tracking with CallFrame entries. Records goal and step execution for debugging and audit.

## CallStack

### API Surface

```csharp
public sealed class CallStack
{
    // Properties
    public int Depth { get; }
    public int MaxDepth { get; }
    public CallFrame? Current { get; }
    public IReadOnlyList<CallFrame> Frames { get; }

    // Constructor
    public CallStack(int maxDepth = 100)

    // Stack operations
    public CallFrame Push(string goalName)
    public CallFrame? Pop()
    public void Clear()

    // Stack trace
    public string GetStackTrace()
}
```

### Behavior & Rules

- `Depth` — current number of frames on the stack
- `MaxDepth` — limit before `CallStackOverflowException` is thrown (default: 100)
- `Current` — the topmost frame, or null if empty
- `Push` creates a new frame and returns it
- `Pop` removes and returns the topmost frame
- `GetStackTrace()` returns formatted string of all frames

### Code Examples

```csharp
var callStack = new CallStack(maxDepth: 50);

// Push frames as goals execute
var frame1 = callStack.Push("MainGoal");
frame1.RecordStep(0, "initialize");

var frame2 = callStack.Push("SubGoal");
frame2.RecordStep(0, "do work");
frame2.RecordStep(1, "validate result");

// Get stack trace
var trace = callStack.GetStackTrace();
// "SubGoal (step 1: validate result)\n  MainGoal (step 0: initialize)"

// Pop when goals complete
callStack.Pop();  // removes SubGoal
callStack.Pop();  // removes MainGoal
```

### Stack Overflow Protection

```csharp
var callStack = new CallStack(maxDepth: 100);

// Recursive goal execution
void ExecuteRecursive(string goalName)
{
    var frame = callStack.Push(goalName);  // Throws CallStackOverflowException at depth 101
    try
    {
        // ... execute goal
        ExecuteRecursive("NestedGoal");
    }
    finally
    {
        callStack.Pop();
    }
}
```

## CallFrame

Represents a single frame in the call stack.

### API Surface

```csharp
public sealed class CallFrame
{
    // Properties
    public string GoalName { get; }
    public int CurrentStepIndex { get; }
    public string? CurrentStepText { get; }
    public DateTime StartedAt { get; }
    public DateTime? CompletedAt { get; }
    public TimeSpan? Duration { get; }
    public List<ExecutedStep> ExecutedSteps { get; }

    // Constructor
    public CallFrame(string goalName)

    // Methods
    public void RecordStep(int index, string? text)
    public void Complete()
}
```

### Behavior & Rules

- `GoalName` — name of the goal being executed
- `CurrentStepIndex` — index of the current/last step
- `CurrentStepText` — text of the current/last step
- `StartedAt` — when the frame was created
- `CompletedAt` — when `Complete()` was called
- `Duration` — computed from `StartedAt` and `CompletedAt`
- `ExecutedSteps` — history of all steps executed in this frame
- `RecordStep` updates current step and adds to history
- `Complete()` sets `CompletedAt` timestamp

### Code Examples

```csharp
var frame = new CallFrame("ProcessOrder");

// As steps execute
frame.RecordStep(0, "validate order");
// do work...
frame.RecordStep(1, "check inventory");
// do work...
frame.RecordStep(2, "process payment");
// do work...

frame.Complete();

Console.WriteLine($"Goal {frame.GoalName} took {frame.Duration?.TotalMilliseconds}ms");
Console.WriteLine($"Executed {frame.ExecutedSteps.Count} steps");
```

## ExecutedStep

Record of a step that was executed.

```csharp
public sealed class ExecutedStep
{
    public int Index { get; }
    public string? Text { get; }
    public DateTime ExecutedAt { get; }
}
```

## Integration with Engine

```csharp
// Engine creates context with CallStack
using var context = engine.CreateContext();
// context.CallStack is automatically created

// During RunGoalAsync
var frame = context.CallStack?.Push(goal.Name);
try
{
    foreach (var step in goal.Steps)
    {
        frame?.RecordStep(step.Index, step.Text);
        await engine.ExecuteStepAsync(step, context);
    }
    frame?.Complete();
}
finally
{
    context.CallStack?.Pop();
}
```

## Optional Tracking

CallStack is optional. If you don't need debugging/audit:

```csharp
// Create context without CallStack (not directly supported in current API)
// The CallStack is always created but you can ignore it

// Or create PLangContext directly
var context = new PLangContext(
    appContext,
    memoryStack: new MemoryStack(),
    callStack: null,  // disable tracking
    cancellationToken: default
);
```

Disabling CallStack provides minor performance improvement by avoiding step recording overhead.

## Relationships

- Stored in [PLangContext](contexts.md)
- Updated by [Engine](engine.md) during goal/step execution
- Contains frames for each [Goal](goals-steps.md) being executed
- Throws `CallStackOverflowException` from [Exceptions](exceptions.md) when limit exceeded
