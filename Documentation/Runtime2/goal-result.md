# GoalResult

The universal return type for all Runtime operations. Wraps success/failure with value and error information.

## API Surface

```csharp
public readonly struct GoalResult
{
    // Properties
    public bool Success { get; }
    public object? Value { get; }
    public ErrorInfo? Error { get; }

    // Static factories
    public static GoalResult Ok(object? value = null)
    public static GoalResult Fail(string message, string key = "Error", int statusCode = 400)
    public static GoalResult Fail(ErrorInfo error)

    // Async helpers
    public static Task<GoalResult> SuccessTask(object? value = null)
    public static Task<GoalResult> FailTask(string message, string key = "Error", int statusCode = 400)

    // Value access
    public T? GetValue<T>()

    // Implicit conversion
    public static implicit operator bool(GoalResult result) => result.Success
}
```

## Behavior & Rules

### Success Results

- `GoalResult.Ok()` — success with no value
- `GoalResult.Ok(value)` — success with value
- `Success` is `true`, `Error` is `null`

### Failure Results

- `GoalResult.Fail(message)` — failure with message
- `GoalResult.Fail(message, key, statusCode)` — failure with full error info
- `GoalResult.Fail(errorInfo)` — failure with pre-constructed ErrorInfo
- `Success` is `false`, `Value` is `null`

### Value Access

- `GetValue<T>()` — returns value cast to T, or default if not compatible
- Implicit bool conversion allows `if (result)` syntax

### Struct Semantics

- `GoalResult` is a `readonly struct` for stack allocation
- Default value has `Success = false`, `Value = null`, `Error = null`

## Code Examples

### Creating Results

```csharp
// Success
var success = GoalResult.Ok();
var successWithValue = GoalResult.Ok(new User { Name = "John" });

// Failure
var notFound = GoalResult.Fail("User not found", "NotFound", 404);
var badRequest = GoalResult.Fail("Invalid email format", "ValidationError", 400);
var serverError = GoalResult.Fail(ErrorInfo.FromException(ex));
```

### Checking Results

```csharp
var result = await engine.RunGoalAsync("CreateUser", context);

// Explicit check
if (result.Success)
{
    var user = result.GetValue<User>();
    Console.WriteLine($"Created: {user?.Name}");
}
else
{
    Console.WriteLine($"Error: {result.Error?.Message}");
}

// Implicit bool conversion
if (result)
{
    // success path
}
```

### Async Helpers

```csharp
public class MyModule : IModule
{
    public Task<GoalResult> ExecuteAsync(string method, object? parameters)
    {
        if (method == "ping")
            return GoalResult.SuccessTask("pong");

        return GoalResult.FailTask($"Unknown method: {method}", "NotFound", 404);
    }
}
```

### Chaining Results

```csharp
var result = await engine.RunGoalAsync("Step1", context);
if (!result)
    return result; // propagate failure

result = await engine.RunGoalAsync("Step2", context);
if (!result)
    return result;

return GoalResult.Ok("All steps completed");
```

## ErrorInfo

Detailed error information for failure results.

### API Surface

```csharp
public sealed class ErrorInfo
{
    // Properties
    public string Id { get; }
    public string Message { get; }
    public string Key { get; }
    public int StatusCode { get; }
    public string? FixSuggestion { get; init; }
    public string? HelpfulLinks { get; init; }
    public DateTime CreatedUtc { get; }
    public Exception? Exception { get; init; }
    public ErrorInfo? InnerError { get; init; }

    // Constructor
    public ErrorInfo(string message, string key = "Error", int statusCode = 400)

    // Static factories
    public static ErrorInfo FromException(Exception ex, string key = "Exception", int statusCode = 500)
    public static ErrorInfo NotFound(string what)
    public static ErrorInfo InvalidInput(string message)
    public static ErrorInfo Unauthorized(string message = "Unauthorized")
}
```

### Behavior & Rules

- `Id` — unique 12-character identifier
- `Key` — error category for programmatic handling
- `StatusCode` — HTTP-like status code
- `FromException` recursively wraps inner exceptions
- `InnerError` allows error chaining

### Code Examples

```csharp
// Create error info
var error = new ErrorInfo("User not found", "NotFound", 404)
{
    FixSuggestion = "Check the user ID and try again",
    HelpfulLinks = "https://docs.plang.dev/errors/not-found"
};

// From exception
try
{
    // operation that throws
}
catch (Exception ex)
{
    return GoalResult.Fail(ErrorInfo.FromException(ex));
}

// Convenience factories
var notFound = ErrorInfo.NotFound("User");           // "User not found", 404
var invalid = ErrorInfo.InvalidInput("Bad email");   // "Bad email", 400
var unauth = ErrorInfo.Unauthorized();               // "Unauthorized", 401
```

## Error Philosophy

The Runtime prefers `GoalResult.Fail` over exceptions for expected failures:

| Scenario | Approach |
|----------|----------|
| Goal not found | `GoalResult.Fail("NotFound")` |
| Validation error | `GoalResult.Fail("ValidationError")` |
| Module method error | `GoalResult.Fail(...)` |
| Null reference in user code | Exception wrapped in `GoalResult.Fail` |
| Stack overflow | `CallStackOverflowException` thrown |

Exceptions are for truly exceptional cases (programming errors, resource exhaustion). Expected operational errors use `GoalResult`.

## Relationships

- Returned by [Engine](engine.md) methods
- Returned by [Module](modules.md) `ExecuteAsync`
- Returned by [IO](io-channels.md) read/write operations
- Contains [ErrorInfo](#errorinfo) for failures
