# Contexts

Runtime2 has a layered context system: app-level shared state, per-request execution state, actor identity, and event scopes.

---

## PLangAppContext

`PLang.Runtime2.Context.PLangAppContext` — **sealed, IDisposable**. One per application lifetime.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `string` | Unique identifier |
| `RootPath` | `string` | Application root directory |
| `Environment` | `string` | Environment name (e.g. `"development"`, `"production"`) |
| `StartedAt` | `DateTime` | When the app started |
| `Events` | `Events` | Global event system (14 event types with pattern matching) |
| `Serializers` | `SerializerRegistry` | Content-type → serializer lookup |
| `IsDebugMode` | `bool` | Debug flag |
| `ShutdownToken` | `CancellationToken` | Signals app shutdown |
| `Uptime` | `TimeSpan` | Computed from `StartedAt` |
| `Keys` | `IEnumerable<string>` | All stored keys |

### Methods

```csharp
T? Get<T>(string key)                          // Retrieve typed value
void Set<T>(string key, T value)               // Store typed value
T GetOrCreate<T>(string key, Func<T> factory)  // Lazy init (atomic)
bool ContainsKey(string key)
bool Remove(string key)
void RequestShutdown()                         // Triggers ShutdownToken
```

### Code Example

```csharp
using var appContext = new PLangAppContext("/path/to/app");
appContext.IsDebugMode = true;

// Store application-wide data
appContext.Set("config", new AppConfig());
var config = appContext.Get<AppConfig>("config");

// Lazy initialization
var cache = appContext.GetOrCreate("cache", () => new ConcurrentDictionary<string, object>());

// Graceful shutdown
appContext.RequestShutdown();
```

---

## PLangContext

`PLang.Runtime2.Context.PLangContext` — **sealed, IDisposable**. One per request / goal execution.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `string` | Unique identifier |
| `AppContext` | `PLangAppContext` | Parent app context |
| `MemoryStack` | `MemoryStack` | Variable storage for this context |
| `CallStack` | `CallStack?` | Execution tracking (optional) |
| `IsAsync` | `bool` | Whether running async |
| `CurrentGoalName` | `string?` | Currently executing goal |
| `CurrentStepIndex` | `int` | Current step index |
| `CreatedAt` | `DateTime` | Context creation time |
| `CancellationToken` | `CancellationToken` | Cancellation support |
| `Parent` | `PLangContext?` | Parent context (for nested calls) |
| `Depth` | `int` | Nesting depth |
| `Actor` | `Actor?` | The actor executing this context |
| `System` | `EventScope` | System-level event scope |
| `User` | `EventScope` | User-level event scope |
| `Goal` | `Goal?` | Currently executing goal |
| `Step` | `Step?` | Currently executing step |
| `Duration` | `TimeSpan` | Computed elapsed time |

### Methods

```csharp
T? Get<T>(string key)                    // Context-scoped storage
void Set<T>(string key, T value)
PLangContext CreateChild()               // New context inheriting parent
PLangContext Clone()                     // Deep copy
void PopulateLoadEvents(Goal goal)       // Wire entity events from global event bindings
void Cancel()                            // Cancel execution
```

### Context Hierarchy

```
PLangAppContext (app lifetime)
  └─ PLangContext (per request)
       ├─ MemoryStack (variables)
       ├─ CallStack (execution tracking)
       ├─ System EventScope
       ├─ User EventScope
       ├─ Actor (identity)
       └─ PLangContext (child, for sub-goal calls)
            └─ ...
```

---

## Actor

`PLang.Runtime2.Context.Actor` — **sealed, IAsyncDisposable**. Represents an execution identity with a trust level.

### TrustLevel

```csharp
public enum TrustLevel
{
    User = 1,     // User-initiated, lowest trust
    Service = 2,  // Service-level operations
    System = 3    // Internal engine, highest trust
}
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Actor name |
| `TrustLevel` | `TrustLevel` | Trust level |
| `Context` | `PLangContext` | Owned context |
| `IO` | `IO` | Owned I/O manager |
| `Engine` | `Engine` | Parent engine |

The engine lazily creates three actors:
- `Engine.System` — `TrustLevel.System`
- `Engine.Service` — `TrustLevel.Service`
- `Engine.User` — `TrustLevel.User`

`Engine.Context` is a shortcut for `Engine.User.Context`.

---

## EventScope

`PLang.Runtime2.Context.EventScope` — lightweight wrapper holding an `Events` instance.

```csharp
public record EventScope
{
    public Events Events { get; init; } = new();
}
```

Each `PLangContext` has two scopes:
- `System` — for system-level event bindings
- `User` — for user-level event bindings

---

## Console vs Web Request Flow

### Console Application

```
Application starts
    → new Engine(fileSystem)
    → engine.LoadGoalsFromDirectoryAsync(buildDir)
    → engine.RunGoalAsync("Start")        // uses Engine.User.Context
    → engine.DisposeAsync()
```

### Web Request (conceptual)

```
Application starts
    → new Engine(fileSystem)
    → engine.LoadGoalsFromDirectoryAsync(buildDir)

Request arrives
    → engine.CreateContext("request-123")
    → context.MemoryStack.Set("request", httpRequest)
    → engine.RunGoalAsync("HandleRequest", context)
    → context.Dispose()
```

## Relationships

- `PLangAppContext` holds global [Events](events.md) system
- `PLangContext` holds [MemoryStack](memory-stack.md) and [CallStack](call-stack.md)
- [Engine](engine.md) creates `PLangContext` via `CreateContext()`
- [Actor](contexts.md) owns a `PLangContext` and `IO`
- Action handlers receive `PLangContext` via `CodeGeneratedExecuteAsync`
