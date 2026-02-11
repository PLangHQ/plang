# Engine

`PLang.Runtime2.Core.Engine` is the central orchestrator. It is a **sealed** class (not partial) implementing `IAsyncDisposable`.

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `string` | Unique 12-char identifier from `Guid.NewGuid().ToString("N")[..12]` |
| `Name` | `string` | Human-readable name (default `"plang"`) |
| `RootPath` | `string` | Root directory of the PLang app |
| `AppContext` | `PLangAppContext` | App-lifetime shared state |
| `Actions` | `ActionRegistry` | Two-level handler lookup (namespace → class → `IClass`) |
| `Serializers` | `SerializerRegistry` | Content-type → serializer routing |
| `Goals` | `Goals` | Goal collection with lazy disk loading |
| `FileSystem` | `IPLangFileSystem` | Abstracted filesystem (never use `System.IO` directly) |
| `IO` | `IO` | Channel-based I/O manager |
| `IsDebugMode` | `bool` | Debug flag |

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
    PLangAppContext appContext,
    ActionRegistry? actions = null,
    SerializerRegistry? serializers = null,
    IPLangFileSystem? fileSystem = null)
```

Both constructors call `RegisterBuiltInModules()` which uses reflection to discover all `IClass` implementations in the assembly and register them with the `ActionRegistry`.

## Goal Execution

```csharp
// Four overloads — all delegate to the core implementation
Task<Data> RunGoalAsync(string goalName, CancellationToken ct = default)
Task<Data> RunGoalAsync(Goal goal, CancellationToken ct = default)
Task<Data> RunGoalAsync(string goalName, PLangContext context, CancellationToken ct = default)
Task<Data> RunGoalAsync(Goal goal, PLangContext context, CancellationToken ct = default)
```

Execution path:
1. Resolve goal by name via `Goals.GetAsync()` (loads from disk if not cached)
2. Returns `Data.Fail(GoalError.NotFound(name))` if goal doesn't exist
3. Call `goal.RunAsync(engine, context, ct)`
4. Returns `Data` — check `Data.Success` / `Data.Error`

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
await using var engine = new Engine(fileSystem);
await engine.LoadGoalsFromDirectoryAsync(buildDir);
var result = await engine.RunGoalAsync("Start");
if (!result.Success)
    Console.Error.WriteLine(result.Error?.Message);
```

`DisposeAsync` cleans up actors and their contexts.

## Relationships

- Creates [PLangContext](contexts.md) via `CreateContext()`
- Holds reference to [PLangAppContext](contexts.md) for app-level configuration
- Uses [ActionRegistry](modules.md) to look up action handlers
- Uses [SerializerRegistry](serializers.md) for data format handling
- Stores [Goals](goals-steps.md) collection
- Returns [Data](goal-result.md) from execution methods
- Owns three [Actors](contexts.md) with different trust levels
