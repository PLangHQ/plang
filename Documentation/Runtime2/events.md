# Events

Runtime2 has two event systems: **entity events** (per Goal/Step/Action) and **global events** (application-wide with pattern matching).

---

## PLang Event Syntax

Events are defined in `events/Events.goal` files placed in your project root. Each step in the Events goal registers one event binding. The builder parses the natural language and maps it to the appropriate EventType and EventScope.

### Event File Location

```
myapp/
├── events/
│   ├── Events.goal           ← runtime events
│   └── BuilderEvents.goal    ← builder-time events
├── Start.goal
└── ...
```

### App Lifecycle Events

```plang
Events
- on app start, call goal SetupApp
- on app end, call goal CleanupApp
- Before app starts, call goal InitializeDatabase
```

The handler goals are defined as separate `.goal` files:

```plang
SetupApp
- set %appName% to "MyApp"
- write out "App initialized"

CleanupApp
- write out "App shutting down"
```

### Goal Events (Before/After)

```plang
Events
/ before every goal runs
- before each goal, call goal BeforeGoal

/ after every goal completes
- after each goal, call goal AfterGoal

/ include private goals (by default only public goals trigger events)
- before each goal, include private, call goal LogGoalStart

/ "before goal ends" means: run after the goal has run (just before it ends)
- before goal ends, include private goals, call goal OnGoalFinishing
```

Handler goals:

```plang
BeforeGoal
- write out "Goal starting: %__Goal.Name__%"

AfterGoal
- write out "Goal completed: %__Goal.Name__%"
```

### Step Events (Before/After)

```plang
Events
/ before every step runs
- before each step, call goal BeforeStep

/ after every step completes
- after each step, call goal AfterStep

/ include private goals in matching
- before each step, include private goals, call goal LogStep
- after each step, include private goals, call goal AfterStep
```

Handler goals:

```plang
BeforeStep
- write out "Before Step"

AfterStep
- write out "After Step"
```

### Goal Load Events

Load events fire when goals and steps are being loaded/parsed, before execution begins.

```plang
Events
/ fires when a goal is being loaded from .pr files
- before goal loads, call goal OnGoalLoading
- after goal loads, call goal OnGoalLoaded

/ fires when a step is being loaded
- before step loads, call goal OnStepLoading
- after step loads, call goal OnStepLoaded

/ fires when an action is being loaded
- before action loads, call goal OnActionLoading
- after action loads, call goal OnActionLoaded
```

### Pattern Matching (Targeting Specific Goals)

```plang
Events
/ only match goals in the api/ folder
- before each goal in api/*, call goal CheckAuthentication

/ only match a specific goal file
- after goal Run.goal, call goal AfterRun

/ match all goals under admin/ path
- before each goal in admin/*, call goal CheckAdminPermission
```

### Error Events

```plang
Events
/ catch any app-level error
- on app error, call goal /events/Runtime/OnAppError

/ catch any step error
- on step error, call goal HandleStepError

/ catch step errors with a specific error key
- on step error, key:"InvalidParameter", call goal HandleInvalidParam
- on step error, key:"PrFileNotFound", include os, call goal HandleMissingPr

/ catch step errors by exception type
- on step error, exception type: PLang.Exceptions.InvalidInstructionFileException, include os, call goal HandleBadInstruction
- on step error, exception type: PLang.Exceptions.MethodNotFoundException, include os, call goal HandleBadInstruction

/ catch goal errors
- on goal error, call goal HandleGoalError

/ catch module errors with a specific status code
- on module error status 402, include private, call goal /apps/Wallet/PaymentRequest
```

### Conditional Events (Startup Parameters)

Events can be restricted to only fire when specific startup parameters are present:

```plang
Events
/ only run when --debug flag is passed at startup
- before each step, include private goals, call Runtime/SendDebug, start parameter --debug
- before goal ends, include private goals, call Runtime/SendDebug, start parameter --debug
- on step error, call /events/Runtime/DebugErrorInIde, start args --debug

/ with named startup args
- before each goal, include private, call Runtime/SendExecutionPath action="start", startup args=--debug
- after each goal, include private, call Runtime/SendExecutionPath action="end", startup args=--debug
```

### Dynamic Events (Registered at Runtime)

Events can also be added dynamically from within goal steps:

```plang
SetupApp
- if %!debug% then
    - set as developer
    - add event on before each step, include private goals, call Runtime/SendDebug
    - before goal ends, include private goals, call Runtime/SendDebug
    - on step error, call /events/Runtime/DebugErrorInIde
```

### Builder Events

Builder events run during the build process, not at runtime. Defined in `events/BuilderEvents.goal`:

```plang
BuildEvents
- before builder ends, call goal CheckGoals
- on step error, key:"InvalidInstructionFile", include os, call goal /events/Runtime/HandleBadInstructionFile
```

### Fire and Forget

Use `dont wait` to run event handlers without waiting for completion:

```plang
Events
- before each goal, call goal TrackAnalytics, dont wait
```

### Complete Example

A full working events setup:

**events/Events.goal**
```plang
Events
- on app start, call goal AppStart
- before each goal, call goal BeforeGoal
- after each goal, call goal AfterGoal
- before each step, include private goals, call goal BeforeStep
- after each step, include private goals, call goal AfterStep
- on app end, call goal AppEnd
- on step error, call goal HandleError
- on app error, call goal HandleAppError
```

**events/EventHandlers.goal**
```plang
AppStart
- write out "Application starting..."

BeforeGoal
- write out "  Goal: %__Goal.Name__% starting"

AfterGoal
- write out "  Goal: %__Goal.Name__% completed"

BeforeStep
- write out "    Step starting"

AfterStep
- write out "    Step completed"

AppEnd
- write out "Application shutting down..."

HandleError
- write out "Step error occurred: %__Error.Message__%"

HandleAppError
- write out "Application error: %__Error.Message__%"
```

### Event Syntax Summary

| PLang Syntax | EventType | EventScope |
|---|---|---|
| `on app start` | Before | StartOfApp |
| `on app end` | After | EndOfApp |
| `before app starts` | Before | StartOfApp |
| `before each goal` | Before | Goal |
| `after each goal` | After | Goal |
| `before goal ends` | After | Goal |
| `before each step` | Before | Step |
| `after each step` | After | Step |
| `before goal loads` | Before | Goal (Load phase) |
| `after goal loads` | After | Goal (Load phase) |
| `before step loads` | Before | Step (Load phase) |
| `after step loads` | After | Step (Load phase) |
| `before action loads` | Before | Action (Load phase) |
| `after action loads` | After | Action (Load phase) |
| `on app error` | After | AppError |
| `on goal error` | After | GoalError |
| `on step error` | After | StepError |
| `on module error` | After | ModuleError |

### Event Modifiers

| Modifier | Description |
|---|---|
| `include private` / `include private goals` | Include private goals in matching |
| `include os` | Include OS/system goals in matching |
| `start parameter --flag` / `startup args=--flag` | Only fire when startup flag is present |
| `dont wait` | Fire and forget (don't wait for handler) |
| `key:"ErrorKey"` | Match errors by key (error events only) |
| `exception type: FullTypeName` | Match errors by exception type |
| `status NNN` | Match errors by HTTP status code |
| `in pattern/*` | Match goals by path pattern |

---

## Entity Events

Each Goal, Step, and Action has an `EntityEvents` property providing Before/After × Load/Run event lists.

### EventList

```csharp
public sealed class EventList
{
    int Count { get; }
    void Add(EventBinding binding)
    bool Remove(EventBinding binding)
    void Clear()
    IReadOnlyList<EventBinding> ToList()
    Task<Data> Run(PLangContext context)  // Executes all handlers in priority-descending order
}
```

### PhaseEvents

```csharp
public sealed class PhaseEvents
{
    EventList Load { get; }   // Fires during Load phase
    EventList Run { get; }    // Fires during Run phase

    void Add(Func<Task<Data>> handler)  // Adds to both phases
}
```

### EntityEvents

```csharp
public sealed class EntityEvents
{
    PhaseEvents Before { get; }   // Before.Load, Before.Run
    PhaseEvents After { get; }    // After.Load, After.Run
}
```

### Event Execution Order

```
Goal.Load()
    Before.Load.Run()    ← entity event list fires
    Steps.Load()
    After.Load.Run()     ← entity event list fires

Goal.RunAsync()
    Before.Run events    ← entity event list fires
    foreach Step
        Before.Run       ← step entity events
        Actions.RunAsync
            Before.Run   ← action entity events
            Execute
            After.Run    ← action entity events
        After.Run        ← step entity events
    After.Run events     ← entity event list fires
```

---

## Global Events

`PLang.Runtime2.Core.Events` — application-wide event system with 14 event types and pattern matching.

### EventType (14 values)

```csharp
public enum EventType
{
    BeforeAppStart,
    AfterAppStart,
    BeforeGoal,
    AfterGoal,
    BeforeStep,
    AfterStep,
    OnError,
    OnVariableChange,
    OnBeforeGoalLoad,
    OnAfterGoalLoad,
    OnBeforeStepLoad,
    OnAfterStepLoad,
    OnBeforeActionLoad,
    OnAfterActionLoad
}
```

### EventBinding

```csharp
public sealed class EventBinding
{
    string Id { get; }                            // 8-char unique identifier
    EventType Type { get; }                       // Which event type
    string? GoalNamePattern { get; }              // Goal name pattern (wildcards)
    string? StepPattern { get; }                  // Step text pattern (wildcards)
    Func<PLangContext, Task<Data>> Handler { get; }  // Handler delegate
    int Priority { get; }                         // Higher = runs first
    bool StopOnError { get; }                     // Stop dispatch chain on error (default: true)
    List<object> Targets { get; }                 // Additional targets

    bool MatchesGoal(string goalName)
    bool MatchesStep(string stepText)
}
```

### Events Class

```csharp
public sealed class Events
{
    StepEventResolver Steps { get; }   // Step-specific event resolution
    GoalEventResolver Goals { get; }   // Goal-specific event resolution

    // Register via binding object
    string Register(EventBinding binding)

    // Register via parameters (convenience)
    string Register(EventType type, Func<PLangContext, Task<Data>> handler,
                    string? goalNamePattern = null, string? stepPattern = null,
                    int priority = 0, bool stopOnError = true)

    bool Unregister(string bindingId)
    void Clear()

    IReadOnlyList<EventBinding> GetBindings(EventType type)
    IReadOnlyList<EventBinding> GetMatchingBindings(EventType type, string? goalName = null, string? stepText = null)

    Task<Data> DispatchAsync(PLangContext context, EventType type, string? goalName = null, string? stepText = null)

    int Count { get; }
}
```

### Event Resolvers

```csharp
// Resolves events for Steps
public sealed class StepEventResolver
{
    EventList Before(Step step, string? goalName = null)
    EventList After(Step step, string? goalName = null)
}

// Resolves events for Goals
public sealed class GoalEventResolver
{
    EventList Before(Goal goal)
    EventList After(Goal goal)
    EventList OnError(Goal goal)
}
```

### Pattern Matching

`GoalNamePattern` and `StepPattern` are case-insensitive and support wildcards:
- `"CreateUser"` — exact match
- `"admin/*"` — prefix match (goals starting with "admin/")
- `"*"` — matches all goals
- `null` or empty — matches everything

`StepPattern` uses substring matching (case-insensitive).

### Priority

Handlers execute in priority-descending order (highest priority first). If `StopOnError` is true and the handler returns a failure, the dispatch chain stops.

### C# Code Examples

```csharp
// Register a global event binding
var events = appContext.Events;

events.Register(
    EventType.BeforeGoal,
    async context => {
        Console.WriteLine($"Goal starting: {context.Goal?.Name}");
        return Data.Ok();
    },
    goalNamePattern: "admin/*",
    priority: 100,
    stopOnError: true
);

events.Register(
    EventType.AfterGoal,
    async context => {
        Console.WriteLine($"Goal completed: {context.Goal?.Name}");
        return Data.Ok();
    }
    // goalNamePattern: null = matches all goals
);

// Dispatch
var result = await events.DispatchAsync(
    context,
    EventType.BeforeGoal,
    goalName: "admin/DeleteUser"
);

if (!result.Success)
{
    // Handler blocked execution
}
```

### Wiring Entity Events from Global Events

`PLangContext.PopulateLoadEvents(goal)` wires global event bindings to entity events during the Load phase. This connects the two event systems:

```
Global Events (EventBinding with patterns)
    ↓ PopulateLoadEvents()
Entity Events (EntityEvents on Goal/Step/Action)
    ↓ Load/Run phases
Handler execution
```

---

## Event Scopes

Each `PLangContext` has two `EventScope` instances:
- `System` — system-level events
- `User` — user-level events

Each scope contains its own `Events` instance. See [Contexts](contexts.md).

## Relationships

- `Events` stored in [PLangAppContext](contexts.md) and `EventScope`
- `EntityEvents` attached to [Goal, Step, Action](goals-steps.md)
- Handlers resolve to goals via [Goals](goals-steps.md)
- Event dispatch returns [Data](goal-result.md)
- `PopulateLoadEvents` bridges global → entity events during [Goal.Load](goals-steps.md)
