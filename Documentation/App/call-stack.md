# CallStack & Debugging

Execution tracking with CallFrame entries. Records goal and step execution for debugging and audit.

## CallStack

`App.Core.CallStack`

### API Surface

```csharp
public sealed class CallStack
{
    // Properties
    int MaxDepth { get; }              // Default: 1000
    bool IsEnabled { get; set; }
    int Depth { get; }
    CallFrame? Current { get; }
    bool IsInEvent { get; }

    // Stack operations
    CallFrame Push(string goalName, string? goalPath = null)
    CallFrame? Pop()
    CallFrame? Peek()
    void Clear()

    // Step recording
    void RecordStep(int index, string? text)

    // Error tracking
    void AddError(IError error)
    IReadOnlyList<IError> GetErrors()
    void ClearErrors()

    // Inspection
    IReadOnlyList<CallFrame> GetFrames()
    string GetStackTrace()
    IReadOnlyList<CallFrame> GetExecutionHistory()
    bool ContainsGoal(string goalName)

    // Serialization
    SerializableCallStack ToSerializable()
}
```

### Behavior & Rules

- `MaxDepth` defaults to **1000** (not 100)
- `IsEnabled` can be toggled at runtime
- `Push` creates a new frame; throws `CallStackOverflowException` if depth exceeds `MaxDepth`
- `Pop` removes and returns the topmost frame
- `RecordStep` records a step in the current frame
- `AddError` / `GetErrors` / `ClearErrors` track errors associated with stack frames
- `ContainsGoal` checks if a goal is anywhere in the current stack (cycle detection)
- `GetExecutionHistory` returns all frames including completed ones
- `ToSerializable` produces a serializable snapshot (`SerializableCallStack` / `SerializableCallFrame`)

### Code Examples

```csharp
var callStack = new CallStack();  // MaxDepth = 1000

// Push frames as goals execute
var frame1 = callStack.Push("MainGoal", "main.goal");
callStack.RecordStep(0, "initialize");

var frame2 = callStack.Push("SubGoal", "sub.goal");
callStack.RecordStep(0, "do work");
callStack.RecordStep(1, "validate result");

// Check stack state
Console.WriteLine($"Depth: {callStack.Depth}");           // 2
Console.WriteLine($"Contains: {callStack.ContainsGoal("MainGoal")}");  // true

// Get stack trace
var trace = callStack.GetStackTrace();

// Pop when goals complete
callStack.Pop();  // removes SubGoal
callStack.Pop();  // removes MainGoal
```

### Stack Overflow Protection

```csharp
// Recursive goal execution is protected
try
{
    var frame = callStack.Push("RecursiveGoal");
    // ... at depth > 1000, throws CallStackOverflowException
}
catch (CallStackOverflowException ex)
{
    Console.WriteLine($"Stack overflow at depth {ex.MaxDepth}");
}
```

## CallFrame

`App.Core.CallFrame` — represents a single frame in the call stack.

### ExecutionPhase

```csharp
public enum ExecutionPhase
{
    Loading,
    Running,
    Completed,
    Failed
}
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `string` | Unique frame identifier |
| `GoalName` | `string` | Name of the goal being executed |
| `GoalPath` | `string?` | Path to the goal file |
| `Phase` | `ExecutionPhase` | Current execution phase |
| `CurrentStepIndex` | `int` | Index of the current/last step |
| `CurrentStepText` | `string?` | Text of the current/last step |
| `Parent` | `CallFrame?` | Parent frame |
| `StartedAt` | `DateTime` | When the frame was created |
| `CompletedAt` | `DateTime?` | When completed |
| `Duration` | `Stopwatch` | Elapsed time |
| `Errors` | `List<IError>` | Errors in this frame |
| `EventId` | `string?` | Associated event (if in event handler) |
| `Indent` | `int` | Nesting indent level |
| `ExecutedSteps` | `List<ExecutedStep>` | History of all steps executed |
| `Depth` | `int` | Stack depth at creation |
| `IsInEvent` | `bool` | Whether this frame is an event handler |
| `MaxStepsPerFrame` | `int` | Step limit per frame (default: 100,000) |

### ExecutedStep

```csharp
public sealed class ExecutedStep
{
    int Index { get; }
    string? Text { get; }
    DateTime StartedAt { get; }
    DateTime? CompletedAt { get; }
    TimeSpan? Duration { get; }
}
```

### Code Examples

```csharp
var frame = callStack.Push("ProcessOrder", "orders/process.goal");

// As steps execute
callStack.RecordStep(0, "validate order");
// do work...
callStack.RecordStep(1, "check inventory");
// do work...
callStack.RecordStep(2, "process payment");
// do work...

Console.WriteLine($"Phase: {frame.Phase}");
Console.WriteLine($"Executed {frame.ExecutedSteps.Count} steps");
Console.WriteLine($"Duration: {frame.Duration.ElapsedMilliseconds}ms");
```

## SerializableCallStack

For serialization and debugging output:

```csharp
public sealed class SerializableCallStack
{
    int Depth { get; }
    List<SerializableCallFrame> Frames { get; }
}

public sealed class SerializableCallFrame
{
    string GoalName { get; }
    string? GoalPath { get; }
    string Phase { get; }
    int CurrentStepIndex { get; }
    string? CurrentStepText { get; }
    DateTime StartedAt { get; }
    List<string> Errors { get; }
}
```

## Integration with Engine

The Engine automatically manages the CallStack during goal and step execution:

```
Goal.RunAsync()
    ├── callStack.Push(goal.Name, goal.Path)     // new frame
    ├── foreach step in Steps
    │   └── Step.RunAsync()
    │       └── callStack.RecordStep(step.Index, step.Text)
    └── callStack.Pop()                            // complete frame
```

## Relationships

- Stored in [PLangContext](contexts.md)
- Updated by [Goal](goals-steps.md) and [Step](goals-steps.md) during execution
- Contains frames for each goal being executed
- Throws `CallStackOverflowException` from [Exceptions](exceptions.md) when limit exceeded
- Error tracking uses [IError](exceptions.md) from the error hierarchy
