# Errors & Exceptions

App has two error mechanisms: the `IError` / `Error` hierarchy for operational errors (returned via `Data.Fail`), and `AppException` types for truly exceptional cases.

---

## IError Interface

`App.Errors.IError`

```csharp
public interface IError
{
    string Id { get; }
    string Message { get; }
    string Key { get; }
    int StatusCode { get; }
    string? FixSuggestion { get; }
    string? HelpfulLinks { get; }
    DateTime CreatedUtc { get; }
    Exception? Exception { get; }
    IError? InnerError { get; }
    string? GoalName { get; }
    int? StepIndex { get; }
}
```

## Error Base Class

`App.Errors.Error` — base implementation of `IError`.

```csharp
public class Error : IError
{
    // Constructors
    public Error(string message, string key = "Error", int statusCode = 400)
    public Error(string message, PLangContext context, string key = "Error", int statusCode = 400)

    // Static factory
    public static Error FromException(Exception ex, string key = "Exception", int statusCode = 500)
}
```

The constructor accepting `PLangContext` automatically populates `GoalName` and `StepIndex` from the context.

## Specialized Error Types

### GoalError

```csharp
public class GoalError : Error
{
    public static GoalError NotFound(string goalName)        // 404
    public static GoalError Cancelled(string goalName)       // 499
}
```

### StepError

```csharp
public class StepError : Error
{
    public Step? Step { get; }

    public static StepError FromException(Exception ex, PLangContext context)
}
```

### ActionError

```csharp
public class ActionError : Error
{
    public string? ActionClass { get; }
    public string? ActionMethod { get; }

    public static ActionError NotFound(string className, string methodName)    // 404
}
```

### ServiceError

```csharp
public class ServiceError : Error
{
    // For handler internal failures
}
```

### Usage

```csharp
// Goal not found
return Data.Fail(GoalError.NotFound("CreateUser"));

// Step exception
catch (Exception ex)
{
    return Data.Fail(StepError.FromException(ex, context));
}

// Action handler not found
return Data.Fail(ActionError.NotFound("variable", "set"));

// Generic error
return Data.Fail(new Error("Invalid input", "ValidationError", 400));

// Error with context
return Data.Fail(new Error("Operation failed", context, "OperationError", 500));
```

---

## Exception Hierarchy

For truly exceptional cases (programming errors, resource exhaustion):

```
Exception
└── AppException
    ├── GoalNotFoundException
    ├── StepExecutionException
    ├── ModuleNotFoundException
    ├── ActionNotFoundException
    ├── VariableNotFoundException
    ├── CallStackOverflowException
    └── SerializationException
```

### AppException

```csharp
public class AppException : Exception
{
    public string Key { get; }
    public int StatusCode { get; }

    public AppException(string message, string key = "AppError", int statusCode = 500)
    public AppException(string message, Exception innerException, string key = "AppError", int statusCode = 500)
}
```

### GoalNotFoundException

```csharp
public class GoalNotFoundException : AppException
{
    public string GoalName { get; }
    // "Goal '{goalName}' not found", key="GoalNotFound", 404
}
```

### StepExecutionException

```csharp
public class StepExecutionException : AppException
{
    public int StepIndex { get; }
    // key="StepExecutionFailed", 500
}
```

### ModuleNotFoundException

```csharp
public class ModuleNotFoundException : AppException
{
    public string ModuleName { get; }
    // "Module '{moduleName}' not found", key="ModuleNotFound", 404
}
```

### ActionNotFoundException

```csharp
public class ActionNotFoundException : AppException
{
    // key="ActionNotFound", 404
}
```

### VariableNotFoundException

```csharp
public class VariableNotFoundException : AppException
{
    public string VariableName { get; }
    // "Variable '{variableName}' not found", key="VariableNotFound", 404
}
```

### CallStackOverflowException

```csharp
public class CallStackOverflowException : AppException
{
    public int MaxDepth { get; }
    // "Call stack overflow: exceeded {maxDepth} frames", key="CallStackOverflow", 500
}
```

### SerializationException

```csharp
public class SerializationException : AppException
{
    public System.Type? TargetType { get; }
    // key="SerializationFailed", 500
}
```

---

## Error vs Exception Philosophy

| Scenario | Mechanism | Why |
|----------|-----------|-----|
| Goal not found | `Data.Fail(GoalError.NotFound(...))` | Expected operational error |
| Action not found | `Data.Fail(ActionError.NotFound(...))` | Expected operational error |
| Validation error | `Data.Fail(new Error(...))` | Expected operational error |
| Step exception | `Data.Fail(StepError.FromException(...))` | Wrapped exception |
| Stack overflow | `CallStackOverflowException` thrown | Programming error (infinite recursion) |
| Null reference | Exception wrapped in `Data.Fail` | Programming error |
| Out of memory | Exception bubbles up | System resource exhaustion |

The Runtime catches exceptions from action execution and wraps them in `Data.Fail`:

```csharp
try
{
    return await handler.CodeGeneratedExecuteAsync(params, app, context);
}
catch (Exception ex)
{
    return Data.Fail(StepError.FromException(ex, context));
}
```

## Handling Errors

```csharp
var result = await app.RunGoalAsync("ProcessOrder");

if (!result.Success)
{
    var error = result.Error;
    Console.WriteLine($"[{error?.Key}] {error?.Message}");

    if (error?.FixSuggestion != null)
        Console.WriteLine($"Fix: {error.FixSuggestion}");
}
```

## Relationships

- `IError` carried by [Data](goal-result.md) via `Error` property
- `GoalError` used by [Goals](goals-steps.md) collection
- `StepError` used by [StepMethods](goals-steps.md) during execution
- `ActionError` used by [ActionMethods](goals-steps.md) during handler lookup
- `CallStackOverflowException` thrown by [CallStack](call-stack.md)
