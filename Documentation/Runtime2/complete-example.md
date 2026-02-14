# Complete Example

End-to-end example showing all major Runtime2 components working together.

## Scenario

A simple application that:
1. Sets variables
2. Writes output
3. Calls a sub-goal
4. Handles errors

## 1. PLang Source Files

### Start.goal
```plang
Start
- set %greeting% to "Hello"
- set %name% to "World"
- call GenerateMessage
- write out %message%
```

### GenerateMessage.goal
```plang
GenerateMessage
- set %message% to "%greeting%, %name%!"
```

## 2. Compiled .pr Format (v0.2)

### Start.pr.json
```json
{
  "name": "Start",
  "visibility": "public",
  "path": "Start.goal",
  "steps": [
    {
      "index": 0,
      "text": "set %greeting% to \"Hello\"",
      "lineNumber": 2,
      "indent": 0,
      "actions": [
        {
          "action": "variable",
          "method": "set",
          "parameters": [
            { "name": "name", "value": "greeting" },
            { "name": "value", "value": "Hello" }
          ]
        }
      ],
      "waitForExecution": true
    },
    {
      "index": 1,
      "text": "set %name% to \"World\"",
      "lineNumber": 3,
      "indent": 0,
      "actions": [
        {
          "action": "variable",
          "method": "set",
          "parameters": [
            { "name": "name", "value": "name" },
            { "name": "value", "value": "World" }
          ]
        }
      ],
      "waitForExecution": true
    },
    {
      "index": 2,
      "text": "call GenerateMessage",
      "lineNumber": 4,
      "indent": 0,
      "actions": [
        {
          "action": "goal",
          "method": "call",
          "parameters": [
            { "name": "goalName", "value": "GenerateMessage" }
          ]
        }
      ],
      "waitForExecution": true
    },
    {
      "index": 3,
      "text": "write out %message%",
      "lineNumber": 5,
      "indent": 0,
      "actions": [
        {
          "action": "output",
          "method": "write",
          "parameters": [
            { "name": "content", "value": "%message%" }
          ]
        }
      ],
      "waitForExecution": true
    }
  ],
  "subGoals": ["GenerateMessage"]
}
```

## 3. Bootstrap the Engine

```csharp
using PLang.Runtime2.Core;
using PLang.Runtime2.Memory;

// Create engine with filesystem
await using var engine = new Engine(fileSystem);

// Load goals from .pr files
await engine.LoadGoalsFromDirectoryAsync(".build");

Console.WriteLine($"Engine ID: {engine.Id}");
Console.WriteLine($"Loaded goals: {string.Join(", ", engine.Goals.Names)}");
```

## 4. Execute

```csharp
// Run the Start goal (uses Engine.User.Context by default)
var result = await engine.RunGoalAsync("Start");

if (result.Success)
{
    Console.WriteLine("Goal completed successfully");

    // Access variables from the memory stack
    var message = engine.MemoryStack.Get<string>("message");
    Console.WriteLine($"Message: {message}");
}
else
{
    Console.WriteLine($"Error: [{result.Error?.Key}] {result.Error?.Message}");
}
```

## 5. Execution Flow

```
engine.RunGoalAsync("Start")
│
├── Goals.GetAsync("Start")
│   └── Deserialize Start.pr.json → Goal
│
└── Goal.RunAsync(engine, context)
    │
    ├── CallStack.Push("Start", "Start.goal")
    │
    ├── Step 0: "set %greeting% to Hello"
    │   └── Actions.RunAsync()
    │       └── Action: variable.set(name="greeting", value="Hello")
    │           ├── Libraries.GetCodeGenerated("variable", "set")
    │           ├── SetHandler.CodeGeneratedExecuteAsync(params, engine, context)
    │           └── MemoryStack: greeting = "Hello"
    │
    ├── Step 1: "set %name% to World"
    │   └── Actions.RunAsync()
    │       └── Action: variable.set(name="name", value="World")
    │           └── MemoryStack: name = "World"
    │
    ├── Step 2: "call GenerateMessage"
    │   └── Actions.RunAsync()
    │       └── Action: goal.call(goalName="GenerateMessage")
    │           │
    │           └── Goal.RunAsync("GenerateMessage")
    │               ├── CallStack.Push("GenerateMessage")
    │               ├── Step 0: variable.set(name="message", value="%greeting%, %name%!")
    │               │   └── Lazy param resolves: greeting="Hello", name="World"
    │               │   └── MemoryStack: message = "Hello, World!"
    │               └── CallStack.Pop()
    │
    ├── Step 3: "write out %message%"
    │   └── Actions.RunAsync()
    │       └── Action: output.write(content="%message%")
    │           └── Lazy param resolves: message="Hello, World!"
    │           └── Output: "Hello, World!"
    │
    └── CallStack.Pop()

Result: Data.Ok()
```

## 6. Error Handling

```csharp
// Run a non-existent goal
var result = await engine.RunGoalAsync("NonExistent");
// result.Success == false
// result.Error is GoalError with Key="GoalNotFound", StatusCode=404

// Run with cancellation
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
result = await engine.RunGoalAsync("LongRunningGoal", cts.Token);
// If cancelled: result.Error is GoalError with Key="Cancelled"

// Check call stack after error
if (!result.Success && engine.Context.CallStack != null)
{
    var trace = engine.Context.CallStack.GetStackTrace();
    Console.WriteLine($"Stack trace:\n{trace}");

    var errors = engine.Context.CallStack.GetErrors();
    foreach (var error in errors)
        Console.WriteLine($"  [{error.Key}] {error.Message}");
}
```

## 7. Handler Pattern (Action Handler)

The `variable.set` handler that executes in step 0:

```csharp
[Action("set", Cacheable = false)]
public partial class Set : IContext
{
    [VariableName]
    public partial string Name { get; init; }
    public partial object? Value { get; init; }
    public partial string? Type { get; init; }

    public Task<Data> Run()
    {
        Context.MemoryStack.Set(Name, Value,
            Type != null ? Memory.Type.FromName(Type) : null);
        return Task.FromResult(Data.Ok(
            new types.variable { name = Name, value = Value, type = Type }));
    }
}
```

The source generator creates partial implementations that:
1. Auto-implement the `partial` properties with lazy `%var%` resolution from MemoryStack
2. Implement `ICodeGenerated.CodeGeneratedExecuteAsync` to wire Context and call `Run()`

## Summary

This example demonstrates:

1. **Engine setup** — creating `Engine`, auto-discovers built-in handlers via `Libraries`
2. **Goal loading** — loading `.pr` files via `LoadGoalsFromDirectoryAsync`
3. **Execution** — `RunGoalAsync` with `Data` result type
4. **Action handlers** — `variable.set` and `output.write` via `Libraries` + `ICodeGenerated`
5. **Lazy parameters** — `%var%` resolved at property access via source-generated records
6. **MemoryStack** — variable storage with `Data` entries
7. **CallStack** — automatic frame tracking during goal/step execution
8. **Error handling** — `Data.Fail` with `IError` hierarchy, stack trace inspection
