# Contexts

Two context types manage state at different lifetimes: `PLangAppContext` for application lifetime and `PLangContext` for per-request/execution lifetime.

## PLangAppContext

Application-level context created once and shared across all executions.

### API Surface

```csharp
public sealed class PLangAppContext : IDisposable
{
    // Properties
    public string RootPath { get; }
    public bool IsDebugMode { get; set; }
    public EventCollection Events { get; }

    // State management
    public T? Get<T>(string key)
    public void Set<T>(string key, T value)
    public T GetOrCreate<T>(string key, Func<T> factory)
    public bool Remove(string key)
    public bool Contains(string key)

    // Constructor
    public PLangAppContext(string rootPath)

    // Disposal
    public void Dispose()
}
```

### Behavior & Rules

- Created once at application startup
- `RootPath` — base directory for the PLang application
- `IsDebugMode` — enables/disables debug features globally
- `Events` — application-wide event registry (see [Events](events.md))
- State storage is thread-safe via `ConcurrentDictionary`
- `GetOrCreate<T>` atomically gets or creates a value

### Code Example

```csharp
using var appContext = new PLangAppContext("/path/to/app");
appContext.IsDebugMode = true;

// Store application-wide data
appContext.Set("config", new AppConfig());
var config = appContext.Get<AppConfig>("config");

// Or use GetOrCreate for lazy initialization
var cache = appContext.GetOrCreate("cache", () => new ConcurrentDictionary<string, object>());
```

## PLangContext

Per-request context created for each goal execution.

### API Surface

```csharp
public sealed class PLangContext : IDisposable
{
    // Properties
    public PLangAppContext AppContext { get; }
    public MemoryStack MemoryStack { get; }
    public CallStack? CallStack { get; }
    public string? CurrentGoalName { get; set; }
    public CancellationToken CancellationToken { get; }

    // Constructor
    public PLangContext(
        PLangAppContext appContext,
        MemoryStack? memoryStack = null,
        CallStack? callStack = null,
        CancellationToken cancellationToken = default)

    // Child context creation
    public PLangContext CreateChild(MemoryStack? memoryStack = null)

    // Disposal
    public void Dispose()
}
```

### Behavior & Rules

- Created per goal execution via `engine.CreateContext()`
- `MemoryStack` — variable storage for this execution (see [MemoryStack](memory-stack.md))
- `CallStack` — execution tracking, may be null if disabled (see [CallStack](call-stack.md))
- `CurrentGoalName` — set by engine during goal execution
- `CancellationToken` — for cooperative cancellation
- `CreateChild()` creates a nested context sharing the same `AppContext`

### Context Lifecycle

```
engine.CreateContext()
    → new PLangContext(appContext, memoryStack, callStack)
    → engine.RunGoalAsync(goal, context)
        → context.CurrentGoalName = goal.Name
        → context.CallStack.Push(goal.Name)
        → execute steps
        → context.CallStack.Pop()
    → context.Dispose()
```

### Code Example

```csharp
// Engine creates context
using var context = engine.CreateContext();

// Or with pre-populated memory
var memory = new MemoryStack();
memory.Set("input", "value");
using var context = engine.CreateContext(memory);

// Access during execution
var value = context.MemoryStack.GetValue("input");
var depth = context.CallStack?.Depth ?? 0;
```

### Child Contexts

```csharp
// Create child context for nested goal execution
using var child = context.CreateChild();
// or with separate memory
using var child = context.CreateChild(new MemoryStack());
```

## Console vs Web Request Flow

### Console Application

```
Application starts
    → new PLangAppContext("/app")
    → new Engine(appContext)

User runs goal
    → engine.CreateContext()
    → engine.RunGoalAsync(goalName, context)
    → context.Dispose()
```

### Web Request (conceptual)

```
Application starts
    → new PLangAppContext("/app")
    → new Engine(appContext)

Request arrives
    → engine.CreateContext()
    → context.MemoryStack.Set("request", httpRequest)
    → engine.RunGoalAsync("HandleRequest", context)
    → context.Dispose()
```

## Relationships

- `PLangAppContext` holds [EventCollection](events.md)
- `PLangContext` holds [MemoryStack](memory-stack.md) and [CallStack](call-stack.md)
- [Engine](engine.md) creates `PLangContext` via `CreateContext()`
- Both contexts are referenced by [Modules](modules.md) via `ModuleContext`
