# Engine

`PLang.Runtime2.Core.Engine` is the central orchestrator. It is a **sealed** class (not partial) implementing `IAsyncDisposable`.

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `string` | Unique 12-char identifier from `Guid.NewGuid().ToString("N")[..12]` |
| `Name` | `string` | Human-readable name (default `"Runtime2"`) |
| `Path` | `string` | Relative root path, always `"/"` |
| `AbsolutePath` | `string` | OS absolute path of the application |
| `Libraries` | `Libraries` | Uniform handler resolution — built-in [0] + external DLLs |
| `Serializers` | `SerializerRegistry` | Content-type → serializer routing |
| `Goals` | `Goals` | Goal collection with lazy disk loading |
| `FileSystem` | `IPLangFileSystem` | Abstracted filesystem (never use `System.IO` directly) |
| `Channels` | `Channels` | Named channel routing (stdin, stdout, stderr, custom) |
| `Events` | `Events` | Global event collection |
| `Cache` | `ICache` | Pluggable step cache (default: in-memory) |
| `Environment` | `string` | Environment name (e.g., "production", "development") |
| `Culture` | `CultureInfo` | Formatting for dates, numbers (default: InvariantCulture) |
| `IsDebugMode` | `bool` | Debug flag |
| `IsTestMode` | `bool` | Test mode flag |
| `StartedAt` | `DateTime` | When the engine was started |
| `Uptime` | `TimeSpan` | How long the engine has been running |
| `ShutdownToken` | `CancellationToken` | For graceful shutdown |

## Actors (Lazy)

The engine creates three actors lazily, each with its own `PLangContext` and trust level:

| Actor | Trust Level | Purpose |
|-------|-------------|---------|
| `System` | `TrustLevel.System` (3) | Internal engine operations |
| `Service` | `TrustLevel.Service` (2) | Service-level operations |
| `User` | `TrustLevel.User` (1) | User-initiated operations |

```csharp
// Convenience — Engine.Context is the User actor's context
public PLangContext Context => User.Context;
public MemoryStack MemoryStack => Context.MemoryStack;
```

## Constructors

```csharp
// Minimal — filesystem only
public Engine(IPLangFileSystem fileSystem)

// Full — all dependencies injectable
public Engine(
    string absolutePath,
    Libraries? libraries = null,
    SerializerRegistry? serializers = null,
    IPLangFileSystem? fileSystem = null,
    string? environment = null)
```

The `Libraries` constructor auto-discovers all `[Action]`-attributed types in the PLang assembly. If no `Libraries` instance is provided, a default one is created with built-in handlers already registered.

## Goal Execution

```csharp
// Multiple overloads — all delegate to the core implementation
Task<Data> RunGoalAsync(GoalCall goalCall, PLangContext? context = null, CancellationToken ct = default)
Task<Data> RunGoalAsync(string goalName, PLangContext? context = null, CancellationToken ct = default)
Task<Data> RunGoalAsync(string goalName, Actor actor, CancellationToken ct = default)
Task<Data> RunGoalAsync(Goal goal, Actor actor, CancellationToken ct = default)
Task<Data> RunGoalAsync(Goal goal, PLangContext? context = null, CancellationToken ct = default)
```

The `GoalCall` overload resolves `%var%` references in the goal name, injects GoalCall parameters into MemoryStack, and tries PrPath first before falling back to name-based lookup.

Execution path:
1. Resolve goal by name via `Goals.GetAsync()` (loads from disk if not cached)
2. Returns `Data.Fail(GoalError.NotFound(name))` if goal doesn't exist
3. Calls `goal.Load(context)` then `goal.RunAsync(engine, context, ct)`
4. Returns `Data` — check `Data.Success` / `Data.Error`

## ResolveAsync

```csharp
Task<object?> ResolveAsync(string key, PLangContext? context = null)
Task<T?> ResolveAsync<T>(string key, PLangContext? context = null)
```

Resolves a value from the engine's key-value store. If the value is a `GoalCall`, executes the goal and returns the result. This enables engine properties to be lazy goal evaluations:

```csharp
engine["Summary"] = new GoalCall { Name = "GetSummary" };
var summary = await engine.ResolveAsync<string>("Summary"); // runs the goal
```

## Goal Loading

```csharp
// Load a single .pr file
Task<Data> LoadGoalFromFileAsync(string prFilePath, PLangContext? context = null, CancellationToken ct = default)

// Load all .pr files from a directory
Task<Data> LoadGoalsFromDirectoryAsync(string directory, string pattern = "*.pr", PLangContext? context = null, CancellationToken ct = default)
```

These delegate to `Goals.LoadFromFileAsync` / `Goals.LoadFromDirectoryAsync`.

## Context Creation

```csharp
PLangContext CreateContext(string? name = null)
```

Creates a new `PLangContext` with the engine's `AppContext`, a fresh `MemoryStack`, and an optional name.

## Lifecycle

```csharp
await using var engine = new Engine(absolutePath);
await engine.LoadGoalsFromDirectoryAsync(buildDir);
var result = await engine.RunGoalAsync("Start");
if (!result.Success)
    Console.Error.WriteLine(result.Error?.Message);
```

`DisposeAsync` cleans up actors, their contexts, and any disposable handlers in Libraries.

## Relationships

- Creates [PLangContext](contexts.md) via `CreateContext()`
- Uses [Libraries](modules.md) to resolve action handlers (replaces ActionRegistry)
- Uses [SerializerRegistry](serializers.md) for data format handling
- Stores [Goals](goals-steps.md) collection
- Returns [Data](goal-result.md) from execution methods
- Owns three [Actors](contexts.md) with different trust levels
