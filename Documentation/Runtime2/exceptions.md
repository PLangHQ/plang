# Exceptions

Custom exception types for runtime errors. Used for truly exceptional cases; expected operational errors use `GoalResult.Fail` instead.

## Exception Hierarchy

```
Exception
└── Runtime2Exception
    ├── GoalNotFoundException
    ├── StepExecutionException
    ├── ModuleNotFoundException
    ├── VariableNotFoundException
    ├── CallStackOverflowException
    └── SerializationException
```

## Runtime2Exception

Base exception for all Runtime errors.

```csharp
public class Runtime2Exception : Exception
{
    public string Key { get; }
    public int StatusCode { get; }

    public Runtime2Exception(string message, string key = "Runtime2Error", int statusCode = 500)
    public Runtime2Exception(string message, Exception innerException, string key = "Runtime2Error", int statusCode = 500)
}
```

### Properties

| Property | Description |
|----------|-------------|
| `Key` | Error category for programmatic handling |
| `StatusCode` | HTTP-like status code |

## GoalNotFoundException

Thrown when a goal cannot be found by name.

```csharp
public class GoalNotFoundException : Runtime2Exception
{
    public string GoalName { get; }

    public GoalNotFoundException(string goalName)
        : base($"Goal '{goalName}' not found", "GoalNotFound", 404)
}
```

### Usage

```csharp
var goal = goals.Get(goalName);
if (goal == null)
    throw new GoalNotFoundException(goalName);
```

**Note:** The Engine typically returns `GoalResult.Fail("NotFound")` instead of throwing this exception.

## ModuleNotFoundException

Thrown when a step references an unregistered module.

```csharp
public class ModuleNotFoundException : Runtime2Exception
{
    public string ModuleName { get; }

    public ModuleNotFoundException(string moduleName)
        : base($"Module '{moduleName}' not found", "ModuleNotFound", 404)
}
```

### Usage

```csharp
var module = modules.Get(step.ModuleName);
if (module == null)
    throw new ModuleNotFoundException(step.ModuleName);
```

**Note:** The Engine typically returns `GoalResult.Fail("ModuleNotFound")` instead of throwing.

## VariableNotFoundException

Thrown when a variable is not found in memory.

```csharp
public class VariableNotFoundException : Runtime2Exception
{
    public string VariableName { get; }

    public VariableNotFoundException(string variableName)
        : base($"Variable '{variableName}' not found", "VariableNotFound", 404)
}
```

## StepExecutionException

Thrown when a step fails to execute.

```csharp
public class StepExecutionException : Runtime2Exception
{
    public int StepIndex { get; }

    public StepExecutionException(string message, int stepIndex)
        : base(message, "StepExecutionFailed", 500)

    public StepExecutionException(string message, int stepIndex, Exception innerException)
        : base(message, innerException, "StepExecutionFailed", 500)
}
```

### Usage

```csharp
try
{
    await module.ExecuteAsync(step.MethodName, step.Parameters);
}
catch (Exception ex)
{
    throw new StepExecutionException($"Step {step.Index} failed: {ex.Message}", step.Index, ex);
}
```

## CallStackOverflowException

Thrown when call stack depth exceeds the configured limit.

```csharp
public class CallStackOverflowException : Runtime2Exception
{
    public int MaxDepth { get; }

    public CallStackOverflowException(int maxDepth)
        : base($"Call stack overflow: exceeded {maxDepth} frames", "CallStackOverflow", 500)
}
```

### Usage

```csharp
public CallFrame Push(string goalName)
{
    if (Depth >= MaxDepth)
        throw new CallStackOverflowException(MaxDepth);

    var frame = new CallFrame(goalName);
    _frames.Push(frame);
    return frame;
}
```

This protects against infinite recursion in PLang goals.

## SerializationException

Thrown when serialization or deserialization fails.

```csharp
public class SerializationException : Runtime2Exception
{
    public Type? TargetType { get; }

    public SerializationException(string message, Type? targetType = null)
        : base(message, "SerializationFailed", 500)

    public SerializationException(string message, Exception innerException, Type? targetType = null)
        : base(message, innerException, "SerializationFailed", 500)
}
```

### Usage

```csharp
try
{
    return await JsonSerializer.DeserializeAsync<T>(stream, options, cancellationToken);
}
catch (JsonException ex)
{
    throw new SerializationException($"Failed to deserialize to {typeof(T).Name}", ex, typeof(T));
}
```

## Exception vs GoalResult Philosophy

| Scenario | Approach | Rationale |
|----------|----------|-----------|
| Goal not found | `GoalResult.Fail` | Expected operational error |
| Module not found | `GoalResult.Fail` | Expected operational error |
| Validation error | `GoalResult.Fail` | Expected operational error |
| Stack overflow | Exception | Programming error (infinite recursion) |
| Null reference | Exception wrapped | Programming error |
| Out of memory | Exception | System resource exhaustion |

The Runtime catches exceptions from module execution and wraps them in `GoalResult.Fail`:

```csharp
try
{
    return await module.ExecuteAsync(method, parameters);
}
catch (Exception ex)
{
    return GoalResult.Fail(ErrorInfo.FromException(ex));
}
```

## Handling Exceptions

```csharp
try
{
    var result = await engine.RunGoalAsync("ProcessOrder", context);
    if (!result.Success)
    {
        // Handle operational error
        Console.WriteLine($"Error: {result.Error?.Message}");
    }
}
catch (CallStackOverflowException ex)
{
    // Handle infinite recursion
    Console.WriteLine($"Stack overflow at depth {ex.MaxDepth}");
}
catch (Runtime2Exception ex)
{
    // Handle other runtime errors
    Console.WriteLine($"[{ex.Key}] {ex.Message}");
}
```

## Relationships

- `CallStackOverflowException` thrown by [CallStack](call-stack.md)
- Other exceptions typically converted to [GoalResult](goal-result.md) failures
- [ErrorInfo](goal-result.md) can wrap exceptions via `FromException`
