# Engine

The central execution object. Orchestrates goal execution, manages module and serializer registries, and creates execution contexts.

## API Surface

```csharp
public partial class Engine : IAsyncDisposable
{
    // Properties
    public string Id { get; }
    public string Name { get; set; }
    public string RootPath { get; }
    public PLangAppContext AppContext { get; }
    public ModuleRegistry Modules { get; }
    public SerializerRegistry Serializers { get; }
    public Goals Goals { get; }
    public bool IsDebugMode { get; set; }

    // Constructor
    public Engine(
        PLangAppContext appContext,
        ModuleRegistry? modules = null,
        SerializerRegistry? serializers = null)

    // Context creation
    public PLangContext CreateContext(MemoryStack? memoryStack = null)

    // Goal execution
    public Task<GoalResult> RunGoalAsync(string goalName, CancellationToken cancellationToken = default)
    public Task<GoalResult> RunGoalAsync(Goal goal, PLangContext context, CancellationToken cancellationToken = default)

    // Step execution
    public Task<GoalResult> ExecuteStepAsync(Step step, PLangContext context, CancellationToken cancellationToken = default)

    // Built-in modules
    public void RegisterBuiltInModules()

    // Disposal
    public async ValueTask DisposeAsync()
}
```

## Behavior & Rules

### Construction

The Engine requires a `PLangAppContext` and optionally accepts custom module and serializer registries:

- `Id` — 12-character unique identifier generated from `Guid.NewGuid().ToString("N")[..12]`
- `Name` — defaults to `"Runtime2"`, can be changed
- `RootPath` — inherited from `AppContext.RootPath`
- `IsDebugMode` — mirrors `AppContext.IsDebugMode`

### Context Creation

`CreateContext()` creates a new `PLangContext` for executing goals:

- Accepts optional `MemoryStack` to pre-populate variables
- Creates fresh `CallStack` for execution tracking
- Context is disposable and should be disposed after use

### Goal Execution

`RunGoalAsync` executes a goal by name or reference:

1. Looks up goal by name if string provided
2. Returns `GoalResult.Fail("NotFound")` if goal doesn't exist
3. Checks cancellation token before execution
4. Sets `context.CurrentGoalName` to the goal name
5. Pushes a `CallFrame` onto the `CallStack`
6. Fires `EventType.BeforeGoal` event
7. Iterates through steps, executing each via `ExecuteStepAsync`
8. If a step fails and `step.CatchError` is false, returns immediately
9. Fires `EventType.AfterGoal` event
10. Pops the `CallFrame`
11. Returns final `GoalResult`

### Step Execution

`ExecuteStepAsync` executes a single step:

1. Looks up module by `step.ModuleName`
2. Returns `GoalResult.Fail("ModuleNotFound")` if not registered
3. Creates `ModuleContext` with engine, goal, step, and context references
4. Calls `module.Initialize(moduleContext)`
5. Records step in `CallStack.Current` if available
6. Calls `module.ExecuteAsync(step.MethodName, step.Parameters)`
7. If `step.ReturnVariable` is set and result is success, stores value in `MemoryStack`
8. Returns result, catching exceptions as `GoalResult.Fail`

### Built-in Modules

`RegisterBuiltInModules()` registers the `VariableModule` with name `"variable"`.

### Disposal

`DisposeAsync` disposes all registered modules that implement `IDisposable` or `IAsyncDisposable`. Safe to call multiple times.

## Code Examples

### Bootstrap Example

```csharp
using var appContext = new PLangAppContext("/app");
await using var engine = new Engine(appContext);
engine.RegisterBuiltInModules();

// Load goals from .pr files
// engine.Goals.Add(loadedGoal);

using var context = engine.CreateContext();
var result = await engine.RunGoalAsync("CreateUser", context);

if (result.Success)
{
    Console.WriteLine($"Result: {result.Value}");
}
else
{
    Console.WriteLine($"Error: {result.Error?.Message}");
}
```

### Custom Module Registration

```csharp
await using var engine = new Engine(appContext);
engine.Modules.Register(new MyCustomModule());
```

### Pre-populated Variables

```csharp
var memoryStack = new MemoryStack();
memoryStack.Set("userId", 123);
memoryStack.Set("userName", "John");

using var context = engine.CreateContext(memoryStack);
var result = await engine.RunGoalAsync("ProcessUser", context);
```

## Relationships

- Creates [PLangContext](contexts.md) via `CreateContext()`
- Holds reference to [PLangAppContext](contexts.md) for app-level configuration
- Uses [ModuleRegistry](modules.md) to look up modules
- Uses [SerializerRegistry](serializers.md) for data format handling
- Stores [Goals](goals-steps.md) collection
- Returns [GoalResult](goal-result.md) from execution methods
