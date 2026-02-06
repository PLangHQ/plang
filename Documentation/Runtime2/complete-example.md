# Complete Example

End-to-end example showing all major Runtime components working together.

## Scenario

A simple user management system that:
1. Creates users with validation
2. Stores user data
3. Logs operations to a channel
4. Tracks execution with events

## 1. Define a Custom Module

```csharp
using PLang.Runtime2.Core;
using PLang.Runtime2.Modules;

public class UserModule : BaseModule
{
    public override string Name => "user";
    public override IEnumerable<string> Aliases => new[] { "users" };

    private readonly List<User> _users = new();
    private int _nextId = 1;

    public override Task<GoalResult> ExecuteAsync(string method, object? parameters)
    {
        return method.ToLowerInvariant() switch
        {
            "create" => CreateAsync(parameters),
            "get" => GetAsync(parameters),
            "list" => ListAsync(),
            "delete" => DeleteAsync(parameters),
            _ => GoalResult.FailTask($"Unknown method: {method}", "MethodNotFound", 404)
        };
    }

    public override bool CanHandle(string method) =>
        method.ToLowerInvariant() is "create" or "get" or "list" or "delete";

    public override IEnumerable<string> GetMethods() =>
        new[] { "create", "get", "list", "delete" };

    private Task<GoalResult> CreateAsync(object? parameters)
    {
        var dict = parameters as Dictionary<string, object?>;
        var name = dict?["name"]?.ToString();
        var email = dict?["email"]?.ToString();

        if (string.IsNullOrEmpty(name))
            return GoalResult.FailTask("Name is required", "ValidationError", 400);

        if (string.IsNullOrEmpty(email))
            return GoalResult.FailTask("Email is required", "ValidationError", 400);

        var user = new User { Id = _nextId++, Name = name, Email = email };
        _users.Add(user);

        return GoalResult.SuccessTask(user);
    }

    private Task<GoalResult> GetAsync(object? parameters)
    {
        var dict = parameters as Dictionary<string, object?>;
        var id = Convert.ToInt32(dict?["id"]);

        var user = _users.FirstOrDefault(u => u.Id == id);
        if (user == null)
            return GoalResult.FailTask($"User {id} not found", "NotFound", 404);

        return GoalResult.SuccessTask(user);
    }

    private Task<GoalResult> ListAsync()
    {
        return GoalResult.SuccessTask(_users.ToList());
    }

    private Task<GoalResult> DeleteAsync(object? parameters)
    {
        var dict = parameters as Dictionary<string, object?>;
        var id = Convert.ToInt32(dict?["id"]);

        var user = _users.FirstOrDefault(u => u.Id == id);
        if (user == null)
            return GoalResult.FailTask($"User {id} not found", "NotFound", 404);

        _users.Remove(user);
        return GoalResult.SuccessTask();
    }
}

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
}
```

## 2. Bootstrap the Engine

```csharp
using PLang.Runtime2.Context;
using PLang.Runtime2.Core;
using PLang.Runtime2.IO;
using PLang.Runtime2.Serialization;

// Create application context
using var appContext = new PLangAppContext("/app");
appContext.IsDebugMode = true;

// Create engine with default registries
await using var engine = new Engine(appContext);

// Register built-in modules
engine.RegisterBuiltInModules();

// Register custom module
engine.Modules.Register(new UserModule());

Console.WriteLine($"Engine ID: {engine.Id}");
Console.WriteLine($"Registered modules: {string.Join(", ", engine.Modules.Names)}");
```

## 3. Register Events

```csharp
// Log all goal executions
appContext.Events.Register(EventType.BeforeGoal, ctx =>
{
    Console.WriteLine($"[START] {ctx.GoalName}");
    return GoalResult.Ok();
});

appContext.Events.Register(EventType.AfterGoal, ctx =>
{
    Console.WriteLine($"[END] {ctx.GoalName}");
    return GoalResult.Ok();
});

// Special handling for admin goals
appContext.Events.Register(EventType.BeforeGoal, ctx =>
{
    Console.WriteLine($"  Admin operation: {ctx.GoalName}");
    return GoalResult.Ok();
}, goalPattern: "Admin*");
```

## 4. Define Goals

```csharp
// CreateUser goal
var createUserGoal = new Goal
{
    Name = "CreateUser",
    Description = "Creates a new user",
    Visibility = GoalVisibility.Public,
    InputParameters = new Dictionary<string, string>
    {
        ["name"] = "string",
        ["email"] = "string"
    },
    Steps = new List<Step>
    {
        new Step
        {
            Index = 0,
            Text = "create user with name=%name%, email=%email%",
            ModuleName = "user",
            MethodName = "create",
            Parameters = new Dictionary<string, object?>
            {
                ["name"] = "%name%",
                ["email"] = "%email%"
            },
            ReturnVariable = "user"
        },
        new Step
        {
            Index = 1,
            Text = "set %result% to %user%",
            ModuleName = "variable",
            MethodName = "set",
            Parameters = new Dictionary<string, object?>
            {
                ["name"] = "result",
                ["value"] = "%user%"
            }
        }
    }
};

// ListUsers goal
var listUsersGoal = new Goal
{
    Name = "ListUsers",
    Description = "Lists all users",
    Visibility = GoalVisibility.Public,
    Steps = new List<Step>
    {
        new Step
        {
            Index = 0,
            Text = "list all users",
            ModuleName = "user",
            MethodName = "list",
            ReturnVariable = "users"
        }
    }
};

// Add goals to engine
engine.Goals.Add(createUserGoal);
engine.Goals.Add(listUsersGoal);

Console.WriteLine($"Loaded goals: {string.Join(", ", engine.Goals.Names)}");
```

## 5. Execute Goals

```csharp
// Create execution context with pre-populated variables
var memoryStack = new MemoryStack();
memoryStack.Set("name", "John Doe");
memoryStack.Set("email", "john@example.com");

using var context = engine.CreateContext(memoryStack);

// Execute CreateUser
Console.WriteLine("\n--- Creating User ---");
var result = await engine.RunGoalAsync("CreateUser", context);

if (result.Success)
{
    var user = result.GetValue<User>();
    Console.WriteLine($"Created user: {user?.Id} - {user?.Name}");
}
else
{
    Console.WriteLine($"Error: {result.Error?.Message}");
}

// Create another user
memoryStack.Set("name", "Jane Smith");
memoryStack.Set("email", "jane@example.com");

result = await engine.RunGoalAsync("CreateUser", context);
if (result.Success)
{
    var user = result.GetValue<User>();
    Console.WriteLine($"Created user: {user?.Id} - {user?.Name}");
}

// List all users
Console.WriteLine("\n--- Listing Users ---");
result = await engine.RunGoalAsync("ListUsers", context);

if (result.Success)
{
    var users = result.GetValue<List<User>>();
    foreach (var user in users ?? new List<User>())
    {
        Console.WriteLine($"  {user.Id}: {user.Name} ({user.Email})");
    }
}
```

## 6. Use IO Channels

```csharp
// Create a logging channel
await using var io = new IO(engine.Serializers);
var logChannel = io.CreateMemoryChannel("log");

// Write log entries
await io.WriteAsync("log", new {
    timestamp = DateTime.UtcNow,
    level = "INFO",
    message = "Application started"
});

await io.WriteAsync("log", new {
    timestamp = DateTime.UtcNow,
    level = "INFO",
    message = $"Created {engine.Goals.Count} goals"
});

// Read log (for demo - reset stream position first)
logChannel.Stream.Position = 0;
var logText = await logChannel.ReadAllTextAsync();
Console.WriteLine($"\n--- Log Output ---\n{logText}");
```

## 7. Check Call Stack

```csharp
// Execute with call stack tracking
using var trackedContext = engine.CreateContext();
trackedContext.CallStack!.Push("MainGoal");

var frame = trackedContext.CallStack.Push("SubGoal");
frame.RecordStep(0, "first step");
frame.RecordStep(1, "second step");
frame.Complete();

Console.WriteLine($"\n--- Call Stack ---");
Console.WriteLine($"Depth: {trackedContext.CallStack.Depth}");
Console.WriteLine($"Stack trace:\n{trackedContext.CallStack.GetStackTrace()}");

trackedContext.CallStack.Pop();
trackedContext.CallStack.Pop();
```

## 8. Error Handling

```csharp
Console.WriteLine("\n--- Error Handling ---");

// Try to run non-existent goal
var errorResult = await engine.RunGoalAsync("NonExistentGoal");
if (!errorResult.Success)
{
    Console.WriteLine($"Expected error: [{errorResult.Error?.Key}] {errorResult.Error?.Message}");
}

// Try to create user without required fields
memoryStack.Set("name", "");
memoryStack.Set("email", "");
errorResult = await engine.RunGoalAsync("CreateUser", context);
if (!errorResult.Success)
{
    Console.WriteLine($"Validation error: [{errorResult.Error?.Key}] {errorResult.Error?.Message}");
}
```

## Complete Output

```
Engine ID: a1b2c3d4e5f6
Registered modules: variable, user

Loaded goals: CreateUser, ListUsers

--- Creating User ---
[START] CreateUser
[END] CreateUser
Created user: 1 - John Doe
[START] CreateUser
[END] CreateUser
Created user: 2 - Jane Smith

--- Listing Users ---
[START] ListUsers
[END] ListUsers
  1: John Doe (john@example.com)
  2: Jane Smith (jane@example.com)

--- Log Output ---
{"timestamp":"2024-01-15T10:30:00Z","level":"INFO","message":"Application started"}
{"timestamp":"2024-01-15T10:30:00Z","level":"INFO","message":"Created 2 goals"}

--- Call Stack ---
Depth: 2
Stack trace:
SubGoal (step 1: second step)
  MainGoal

--- Error Handling ---
Expected error: [NotFound] Goal 'NonExistentGoal' not found
Validation error: [ValidationError] Name is required
```

## Summary

This example demonstrates:

1. **Engine setup** — creating `PLangAppContext` and `Engine`
2. **Module registration** — built-in and custom modules
3. **Event handling** — before/after goal events with patterns
4. **Goal definition** — programmatic goal and step creation
5. **Execution** — running goals with `PLangContext` and `MemoryStack`
6. **IO channels** — writing/reading data through named channels
7. **Call stack** — tracking execution for debugging
8. **Error handling** — checking `GoalResult` for failures
