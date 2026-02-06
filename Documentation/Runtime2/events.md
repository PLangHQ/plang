# Events

Event dispatching for goal lifecycle hooks. Register handlers to run before/after goal execution.

## EventCollection

### API Surface

```csharp
public enum EventType
{
    BeforeGoal,
    AfterGoal
}

public sealed class EventCollection
{
    // Registration
    public void Register(EventType type, Func<EventContext, Task<GoalResult>> handler, string? goalPattern = null)
    public void Register(EventType type, Func<EventContext, GoalResult> handler, string? goalPattern = null)
    public bool Unregister(EventType type, Func<EventContext, Task<GoalResult>> handler)

    // Dispatch
    public Task<GoalResult> DispatchAsync(EventType type, object? data, string? goalName = null, CancellationToken cancellationToken = default)

    // Query
    public bool HasHandlers(EventType type)
    public int HandlerCount(EventType type)
}
```

### Behavior & Rules

- Handlers are invoked in registration order
- `goalPattern` — if null, handler fires for all goals; if set, only for matching goal names
- Pattern matching is case-insensitive and supports wildcards:
  - `"CreateUser"` — exact match
  - `"admin/*"` — matches goals starting with "admin/"
  - `"*"` — matches all goals
- `DispatchAsync` executes all matching handlers
- If any handler returns a failed `GoalResult`, dispatch continues but the error is propagated
- Sync handlers are wrapped in `Task.FromResult`

## EventContext

```csharp
public sealed class EventContext
{
    public EventType Type { get; }
    public object? Data { get; }
    public string? GoalName { get; }
    public CancellationToken CancellationToken { get; }
}
```

The `Data` property typically contains the `PLangContext` being used for execution.

## Code Examples

### Basic Event Registration

```csharp
var events = new EventCollection();

// Register before-goal handler
events.Register(EventType.BeforeGoal, ctx =>
{
    Console.WriteLine($"Starting goal: {ctx.GoalName}");
    return GoalResult.SuccessTask();
});

// Register after-goal handler
events.Register(EventType.AfterGoal, ctx =>
{
    Console.WriteLine($"Finished goal: {ctx.GoalName}");
    return GoalResult.SuccessTask();
});
```

### Pattern-Based Registration

```csharp
// Only for specific goal
events.Register(EventType.BeforeGoal, ctx =>
{
    Console.WriteLine("Creating user...");
    return GoalResult.SuccessTask();
}, goalPattern: "CreateUser");

// For all admin goals
events.Register(EventType.BeforeGoal, ctx =>
{
    // Check permissions
    var context = ctx.Data as PLangContext;
    if (!IsAdmin(context))
        return GoalResult.FailTask("Unauthorized", "Unauthorized", 401);
    return GoalResult.SuccessTask();
}, goalPattern: "admin/*");
```

### Sync Handlers

```csharp
// Sync handler (wrapped internally)
events.Register(EventType.AfterGoal, ctx =>
{
    LogGoalCompletion(ctx.GoalName);
    return GoalResult.Ok();
});
```

### Dispatching Events

```csharp
// Engine dispatches events during execution
var result = await events.DispatchAsync(
    EventType.BeforeGoal,
    data: context,
    goalName: goal.Name,
    cancellationToken: ct
);

if (!result.Success)
{
    // Handler blocked execution
    return result;
}
```

### Unregistering Handlers

```csharp
Func<EventContext, Task<GoalResult>> handler = ctx =>
{
    // handler logic
    return GoalResult.SuccessTask();
};

events.Register(EventType.BeforeGoal, handler);

// Later...
events.Unregister(EventType.BeforeGoal, handler);
```

## Event Flow in Engine

```
Engine.RunGoalAsync(goal, context)
    │
    ├── appContext.Events.DispatchAsync(EventType.BeforeGoal, context, goal.Name)
    │   │
    │   ├── handler 1 (pattern: null = all goals)
    │   ├── handler 2 (pattern: "CreateUser" = exact match)
    │   └── handler 3 (pattern: "admin/*" = prefix match)
    │
    ├── Execute goal steps...
    │
    └── appContext.Events.DispatchAsync(EventType.AfterGoal, context, goal.Name)
        │
        └── handlers...
```

## Use Cases

| Use Case | Event Type | Pattern |
|----------|------------|---------|
| Logging all goals | BeforeGoal/AfterGoal | null |
| Authentication check | BeforeGoal | "admin/*" |
| Performance timing | BeforeGoal + AfterGoal | null |
| Specific goal setup | BeforeGoal | "CreateUser" |
| Cleanup after goal | AfterGoal | specific pattern |

## Relationships

- Stored in [PLangAppContext](contexts.md) as `Events` property
- Dispatched by [Engine](engine.md) during goal execution
- Handlers receive [PLangContext](contexts.md) as event data
- Return [GoalResult](goal-result.md) to indicate success/failure
