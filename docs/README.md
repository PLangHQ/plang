# PLang Runtime - New Architecture

## Design Philosophy

### The Problem with Traditional Approaches

Traditional programming involves **parameter explosion** - modules with many specialized methods, each with many parameters:

```csharp
// OLD - Parameter explosion
public Task SetString(string name, string value)
public Task SetInt(string name, int value)
public Task SetList(string name, List<object> value)
public Task HttpPost(string url, string body, Dictionary<string,string> headers, int timeout, string contentType)
public Task InsertIntoTable(string table, string col1, string col2, string col3...)
```

This creates problems:
- LLM must map natural language to specific method + many parameters
- Many methods = many bugs
- PLang syntax becomes complex
- Changes ripple through the system

### The Object-Based Solution

Instead, we pass **one cohesive object** that contains everything needed:

```csharp
// NEW - Object-based
public Task<GoalResult> Execute(string method, HttpRequest? data)

public record HttpRequest(string Url, object? Data = null, Dictionary<string, string>? Headers = null)
{
    public string Method { get; set; } = "GET";
    public int? Timeout { get; set; }
    public string? ContentType { get; set; }
}
```

### Key Principles

1. **Objects Flow Through the System**
   - No decomposition at module boundaries
   - Pass the object in, get a result out
   - The receiver accesses what it needs

2. **Typed Request Objects**
   - Not `object?` everywhere - use typed records
   - `HttpRequest`, `DbQuery`, `DbInsert` etc.
   - One object per operation type

3. **Convenience Methods Call Execute**
   ```csharp
   public Task<GoalResult> Get(HttpRequest request)
   {
       request.Method = "GET";
       return Execute("get", request);
   }
   ```

4. **Injectable Executors**
   - Core logic can be replaced via DLL or goal path
   - `engine.Http.SetExecutor("my-http.dll")`
   - `engine.Http.SetExecutor("/custom/HttpExecutor")`

5. **Stream-Based Serialization**
   - Serializers work with streams, not byte arrays
   - Content type determines serializer
   - `engine.Serializers["application/json"].Serialize(data, stream)`

6. **GoalResult for Everything**
   - Never return `Task<object?>` - return `Task<GoalResult>`
   - `GoalResult.Success(data)` or `GoalResult.Error(message)`
   - Errors flow through channels: `result.Channels.Error`

### Benefits

1. **LLM Builder Job Becomes Trivial**
   - Just identify: action, target, object, output variable
   - Map to typed request record
   - No parameter matching

2. **Less C# Code = Fewer Bugs**
   - One entry point per module
   - Logic lives in Execute, not spread across methods

3. **PLang Stays Natural**
   ```plang
   - get http://api.com/users, write to %users%
   - insert into users %userData%, write to %id%
   ```
   Not:
   ```plang
   - http post url="http://api.com" body=%data% headers=%headers% timeout=30
   ```

4. **CallStack Is Optional**
   - Enable only when debugging
   - `goal.EnableCallStack()` 
   - Steps inherit from parent - no performance cost when disabled

5. **Variable Changes Are Tracked**
   - Clone before state on change
   - Enables debugging, undo, audit trails

---

## Overview

This is the new PLang runtime architecture based on object-based programming principles. The core idea: **pass objects directly between operations, let C# modules handle complexity internally**.

## Key Concepts

### Engine

The central runtime that coordinates everything:

```csharp
var engine = new Engine();

// Run a goal
var result = await engine.Run("/CreateUser", new { name = "John", age = 30 });

// Check result
if (result.IsError)
{
    Console.WriteLine(result.Channels.Error?.Message);
}
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `System` | `PLangAppContext` | Application-level context (lives for app lifetime) |
| `User` | `PLangContext` | Request/execution-level context |
| `Out` | `IO` | Output channels |
| `In` | `IO` | Input channels |
| `Goals` | `Goals` | Collection of loaded goals |
| `CallStack` | `CallStack` | Execution call stack |
| `Events` | `EventCollection` | Event handlers |
| `MemoryStack` | `MemoryStack` | Variable storage |
| `Serializers` | `SerializerRegistry` | Registered serializers |

## TypeInfo Record

Types are represented as a record with short and full names:

```csharp
public record TypeInfo(string ShortName)
{
    public string? FullName { get; set; }
}

// Usage
engine.MemoryStack.Set("user", userData, new TypeInfo("object") { FullName = "MyApp.User" });

// Or implicit from string
engine.MemoryStack.Set("count", 42, "int");
```

## IO and Channels

IO inherits from `Stream` and provides named channels:

```csharp
// Write to default channel
await engine.Out.WriteAsync(data);

// Write to named channel
await engine.Out.WriteAsync(data, "debug");

// Access channel directly
var debugChannel = engine.Out["debug"];

// Bind channel to a goal
engine.Out["debug"].Bind(debugGoal);

// Bind channel to a handler
engine.Out["debug"].Bind(async (data) => {
    Console.WriteLine(data);
    return GoalResult.Success();
});
```

### Serializers

Register custom serializers:

```csharp
// Add from DLL
engine.Serializers.Add("myserializer.dll");

// Add by type name
engine.Serializers.Add("MyNamespace.MySerializer");

// Register directly
engine.Serializers.Register("msgpack", new MsgPackSerializer());

// Use when writing
await engine.Out.WriteAsync(data, "default", "msgpack");
```

## Modules

Modules follow the object-based pattern: typed request objects, convenience methods, injectable executors.

### Module Pattern

```csharp
public class HttpModule : BaseModule
{
    public override string Name => "http";
    
    // Injectable executor - can be DLL or goal path
    private Func<HttpRequest, Task<GoalResult>>? _customExecutor;
    
    public void SetExecutor(string pathOrDll)
    {
        if (pathOrDll.EndsWith(".dll"))
            _customExecutor = LoadFromDll(pathOrDll);
        else
            _customExecutor = async (req) => await Engine.Run(pathOrDll, req);
    }
    
    // Typed convenience methods
    public Task<GoalResult> Get(HttpRequest request)
    {
        request.Method = "GET";
        return Execute("get", request);
    }
    
    public Task<GoalResult> Post(HttpRequest request)
    {
        request.Method = "POST";
        return Execute("post", request);
    }
    
    // Execute receives typed object, returns GoalResult
    public override Task<GoalResult> Execute(string method, object? data)
    {
        if (data is not HttpRequest request)
            return GoalResult.ErrorTask("HttpRequest is required");
        
        if (_customExecutor != null)
            return _customExecutor(request);
        
        return ExecuteRequest(request);
    }
    
    protected virtual async Task<GoalResult> ExecuteRequest(HttpRequest request)
    {
        // Use stream-based serialization
        var contentType = request.ContentType ?? "application/json";
        using var stream = new MemoryStream();
        Serializers[contentType].Serialize(request.Data, stream);
        
        // ... execute HTTP request ...
        
        return GoalResult.Success(result);
    }
}
```

### Typed Request Objects

```csharp
// HTTP
public record HttpRequest(string Url, object? Data = null, Dictionary<string, string>? Headers = null)
{
    public string Method { get; set; } = "GET";
    public int? Timeout { get; set; }
    public string? ContentType { get; set; }
}

// Database
public record DbQuery(string Sql, object? Parameters = null)
{
    public string? DataSource { get; set; }
    public bool ReturnOne { get; set; }
}

public record DbInsert(string Table, object Data)
{
    public string? DataSource { get; set; }
    public string? WriteTo { get; set; }
}
```

### BaseModule Properties

When inheriting from `BaseModule`, you have access to:

```csharp
public Engine Engine { get; set; }     // Full engine reference
public Goal Goal { get; set; }         // Current executing goal
public Step Step { get; set; }         // Current step

// Convenience accessors
protected MemoryStack MemoryStack => Engine.MemoryStack;
protected PLangAppContext System => Engine.System;
protected PLangContext User => Engine.User;
protected IO Out => Engine.Out;
protected IO In => Engine.In;
protected SerializerRegistry Serializers => Engine.Serializers;
```

### GoalResult Returns

Always return `GoalResult`, never `object?`:

```csharp
// Success
return GoalResult.Success(data);
return GoalResult.SuccessTask(data);  // Returns Task<GoalResult>

// Error
return GoalResult.Error("Something went wrong");
return GoalResult.Error("Not found", statusCode: 404);
return GoalResult.ErrorTask("Bad request", statusCode: 400);

// In async methods
return await GoalResult.SuccessTask(data);
```

## CallStack Inheritance

CallStack is optional and inherits from parent to child:

```csharp
// Enable CallStack on a specific goal
var goal = engine.Goals["/CreateUser"];
goal.EnableCallStack();

// Now steps in this goal will use the same CallStack
// Steps can also have their own CallStack if needed

// Enable CallStack on all goals
engine.Goals.EnableCallStack();
```

When a step executes:
1. If step has its own CallStack, use it
2. Otherwise, inherit from parent Goal's CallStack
3. If neither has CallStack, no tracking (faster execution)

## Goals and Steps

### Loading Goals

```csharp
// Load single .pr file
engine.Goals.Load("path/to/goal.pr");

// Load directory
engine.Goals.LoadDirectory("./goals", "*.pr");

// Access by path
var goal = engine.Goals["/CreateUser"];
```

### Goal Structure

Goals are partial classes with before/after events:

```csharp
var goal = engine.Goals["/CreateUser"];

// Subscribe to events
goal.BeforeRun += async (g, parameters) => {
    Console.WriteLine($"Running: {g.Path}");
};

goal.AfterRun += async (g, parameters, result) => {
    Console.WriteLine($"Completed: {result.IsSuccess}");
};
```

## GoalResult

Every goal/step returns a `GoalResult`:

```csharp
public readonly struct GoalResult
{
    public string Type { get; }        // "goal" or "error"
    public object? Data { get; }       // Result data
    public ChannelData Channels { get; } // Channel-specific data
    
    public bool IsSuccess => Type == "goal";
    public bool IsError => Type == "error";
}
```

Access error via channels:

```csharp
var result = await engine.Run("/SomeGoal");
if (result.IsError)
{
    var error = result.Channels.Error;
    Console.WriteLine(error?.Message);
    Console.WriteLine(error?.StackTrace);
}

// Or access channel-specific data
var debugData = result.Channels["debug"];
```

## MemoryStack

Variable storage with change tracking:

```csharp
// Set variable with type info
engine.MemoryStack.Set("user", userData, new TypeInfo("object") { FullName = "MyApp.User" });

// Set with simple type
engine.MemoryStack.Set("count", 42, "int");

// Get variable
var user = engine.MemoryStack.Get("user");
var value = user?.Value;
var type = user?.Type;  // TypeInfo record

// Subscribe to changes (only fires when CallStack is enabled)
engine.Events.OnVariableChanging((key, before, after) => {
    Console.WriteLine($"{key}: {before?.Value} -> {after?.Value}");
});

engine.Events.OnVariableChanged((key, before, after) => {
    // After the change - before value is cloned
});
```

## Events

Register goal-level events:

```csharp
// Before any goal
engine.Events.AddBefore(null, async goal => {
    Console.WriteLine($"Starting: {goal.Path}");
});

// Before goals matching pattern
engine.Events.AddBefore("/admin/.*", async goal => {
    // Check admin permissions
});

// After goals (async = fire and forget)
engine.Events.AddAfter(null, async goal => {
    // Log completion
}, isAsync: true);

// After goals (sync = await)
engine.Events.AddAfter(null, async goal => {
    // Must complete before continuing
}, isAsync: false);
```

## .pr File Format

```json
{
  "path": "/CreateUser",
  "steps": [
    {
      "line": 1,
      "text": "validate %name%(type:object) is not empty",
      "module": "validation",
      "method": "notEmpty"
    },
    {
      "line": 2,
      "text": "insert into users, name=%name%, write to %user%",
      "module": "db",
      "method": "insert"
    },
    {
      "line": 3,
      "text": "write out %user%",
      "module": "io",
      "method": "write"
    }
  ]
}
```

## Complete Example

```csharp
// Create engine
var engine = new Engine();

// Register modules
ModuleRegistry.Register("db", new DbModule());
ModuleRegistry.Register("io", new IoModule());
ModuleRegistry.Register("validation", new ValidationModule());

// Add custom serializer
engine.Serializers.Add("msgpack.dll");

// Setup debug channel
engine.Out["debug"].Bind(async data => {
    Console.WriteLine($"[DEBUG] {data}");
    return GoalResult.Success();
});

// Setup events
engine.Events.AddBefore(null, async goal => {
    Console.WriteLine($"Running: {goal.Path}");
});

engine.Events.OnVariableChanged((key, before, after) => {
    Console.WriteLine($"Variable changed: {key}");
});

// Load goals
engine.Goals.LoadDirectory("./goals");

// Enable call stack tracking for debugging
engine.Goals.EnableCallStack();

// Run
var result = await engine.Run("/CreateUser", new { 
    name = "John", 
    email = "john@example.com" 
});

if (result.IsSuccess)
{
    Console.WriteLine($"Created user: {result.Data}");
}
else
{
    Console.WriteLine($"Error: {result.Channels.Error?.Message}");
}
```

## File Structure

```
PLang/Runtime/
├── Engine.cs              # Main runtime engine
├── Contexts.cs            # PLangAppContext, PLangContext
├── IO.cs                  # IO stream with channels
├── Channel.cs             # Named channel (also Stream)
├── Goals.cs               # Goal collection
├── Goal.cs                # Goal class (CallStack optional)
├── Step.cs                # Step class (inherits CallStack from Goal)
├── GoalResult.cs          # Result struct with Task helpers
├── ChannelData.cs         # Channel-specific result data
├── ErrorInfo.cs           # Error information struct
├── CallStack.cs           # Execution call stack
├── CallFrame.cs           # Single call frame
├── EventCollection.cs     # Event management
├── MemoryStack.cs         # Variable storage
├── ObjectValue.cs         # Variable wrapper with TypeInfo
├── Properties.cs          # List of ObjectValue
├── SerializerRegistry.cs  # Stream-based serializer management
├── ModuleRegistry.cs      # Module management + BaseModule
├── TypeMapping.cs         # Type name mapping
├── Exceptions.cs          # Custom exceptions
├── GoalData.cs            # JSON deserialization DTOs
└── Modules/
    ├── HttpModule.cs      # Example HTTP module with typed requests
    └── DbModule.cs        # Example DB module with typed requests
```

## Questions for Claude Code CLI

When implementing this architecture, consider:

1. **Does the `GoalResult` pattern make sense?** Returning `{ type:"goal"|"error", data, channels }` instead of exceptions?

2. **Stream-based serialization** - `Serializers[contentType].Serialize(data, stream)` vs byte arrays?

3. **CallStack inheritance** - Steps inherit from parent Goal. Is this the right granularity?

4. **Injectable executors** - `module.SetExecutor("path.dll")` or `module.SetExecutor("/goal/path")` - any concerns?

5. **TypeInfo record** - `Set(name, value, new TypeInfo("string") { FullName = "System.String" })` - is this sufficient for type metadata?

## Design Philosophy Summary

### Object-Based Programming

| Old Pattern | New Pattern |
|------------|-------------|
| Many methods with many parameters | One `Execute(method, data)` with typed request object |
| `HttpPost(url, body, headers, timeout)` | `Execute("post", HttpRequest)` |
| `InsertIntoTable(table, col1, col2, ...)` | `Execute("insert", DbInsert)` |
| Returns `Task<object?>` | Returns `Task<GoalResult>` |
| Byte array serialization | Stream-based serialization |
| CallStack always on | CallStack optional, inherited |

### The Key Insight

The LLM's job becomes trivial:
1. Identify the action (get, post, insert, select)
2. Identify the target (url, table)
3. Build the typed request object
4. Return GoalResult

The C# module handles the complexity internally.
