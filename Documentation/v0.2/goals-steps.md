# Goals, Steps, and Actions

These are the three entity types that form the execution model. Each is a **sealed partial class** split across a properties file and a methods file.

---

## Goal

`App.Core.Goal` — sealed partial class (`Goal.cs` + `GoalMethods.cs`).

### Properties (Goal.cs)

| Property | Type | Stored | Description |
|----------|------|--------|-------------|
| `Name` | `string` | Yes | Goal name (from file heading) |
| `Description` | `string?` | Yes | Optional description |
| `Comment` | `string?` | Yes | Builder comment |
| `Steps` | `Steps` | Yes | Ordered step collection |
| `SubGoals` | `List<string>` | Yes | Referenced sub-goal names |
| `Visibility` | `Visibility` | Yes | `Private` (0) or `Public` (1) |
| `Path` | `string?` | Yes | Relative path to `.goal` file |
| `PrPath` | `string?` | Yes | Computed from `Path` (inserts `.build/`, lowercases, `.pr` extension) |
| `Hash` | `string?` | Yes | Content hash for change detection |
| `IsSetup` | `bool` | Yes | Runs during setup phase |
| `IsEvent` | `bool` | Yes | This goal is an event handler |
| `InputParameters` | `Dictionary<string, string>?` | Yes | Named input parameters |
| `Parent` | `Goal?` | No | Parent goal (`[JsonIgnore]`) |
| `App` | `App?` | No | App reference (`[JsonIgnore]`) |
| `Events` | `EntityEvents` | No | Before/After × Load/Run event lists |
| `Errors` | `List<Info>` | Yes | Build errors |
| `Warnings` | `List<Info>` | Yes | Build warnings |
| `FullPath` | `string` | No | Computed: `Parent.FullPath/Name` |

### PrPath Computation

`PrPath` is a computed property derived from `Path`. It inserts `.build` as a subfolder and lowercases the filename:

```
Path = "users/CreateUser.goal"  →  PrPath = "users/.build/createuser.pr"
Path = "Start.goal"             →  PrPath = ".build\start.pr"
```

The setter is empty — PrPath is always derived from Path.

### Methods (GoalMethods.cs)

```csharp
// Load phase — wires events, calls Steps.Load
Task Load(PLangContext context)

// Run phase — executes all steps sequentially
Task<Data> RunAsync(App app, PLangContext context, CancellationToken ct = default)

// Format for LLM consumption (Scriban template or fallback)
string FormatForLlm()

// Convert back to goal text
string ToText()

// Factory for not-found placeholder
static Goal NotFound(string name)
```

**Load sequence:**
1. `PopulateLoadEvents(goal)` — wire entity events from global bindings
2. `Before.Load.Run()` — fire before-load events
3. `Steps.Load(context)` — load each step
4. `After.Load.Run()` — fire after-load events

**Run sequence:**
1. Set `context.Goal` and `context.CurrentGoalName`
2. Check cancellation
3. `Before.Run` events
4. `CallStack.Push(frame)`
5. Iterate steps → `step.RunAsync(app, context, ct)`
6. `After.Run` events
7. `CallStack.Pop()`
8. Return `Data.Ok()`

---

## Step

`App.Core.Step` — sealed partial class (`Step.cs` + `StepMethods.cs`).

### Properties (Step.cs)

| Property | Type | Stored | Description |
|----------|------|--------|-------------|
| `Index` | `int` | Yes | Position in goal (0-based) |
| `Text` | `string` | Yes | The PLang step text |
| `LineNumber` | `int` | Yes | Line in source file |
| `Indent` | `int` | Yes | Indentation level |
| `Comment` | `string?` | Yes | Builder comment |
| `Actions` | `Actions` | Yes | Action bindings for this step |
| `Hash` | `string?` | Yes | Content hash |
| `Intent` | `string?` | Yes | LLM-inferred intent |
| `Errors` | `List<Info>` | Yes | Build errors |
| `Warnings` | `List<Info>` | Yes | Build warnings |
| `WaitForExecution` | `bool` | Yes | Whether to await completion |
| `Disabled` | `bool` | No | Set by `condition.if` on indented sub-steps (`[JsonIgnore]`, context-backed) |
| `Goal` | `Goal?` | No | Parent goal (`[JsonIgnore]`) |
| `Events` | `EntityEvents` | No | Before/After × Load/Run event lists |

**Important:** Steps do NOT have `ModuleName` or `MethodName` directly. The module/method binding is on each `Action` within the step's `Actions` collection. Error handling, caching, and timeouts are **not** step-level properties either — they're `[Modifier]`-attributed actions attached to individual actions via `Action.Modifiers`. See [architecture.md](architecture.md#action-modifiers).

### Methods (StepMethods.cs)

```csharp
Task Load(PLangContext context)
Task<Data> RunAsync(App app, PLangContext context, CancellationToken ct = default)
Step Clone()
```

**Run sequence:**
1. Set `context.Step`
2. `CallStack.RecordStep()`
3. `Before.Run` events
4. `Actions.RunAsync(app, context, ct)` — executes all actions
5. `After.Run` events
6. Catches exceptions → wraps as `StepError`

---

## Action

`App.Core.Action` — sealed partial class (`Action.cs` + `ActionMethods.cs`).

### Properties (Action.cs)

| Property | Type | Stored | JSON Name | Description |
|----------|------|--------|-----------|-------------|
| `Class` | `string` | Yes | `"action"` | Handler class name |
| `Method` | `string` | Yes | `"method"` | Handler method name |
| `Parameters` | `List<Data>` | Yes | `"parameters"` | Input parameters |
| `Modifiers` | `Modifiers` | Yes | `"modifiers"` | Wrapper actions (cache/timeout/error) folded around this action at runtime |
| `Errors` | `List<Info>` | Yes | | Build errors |
| `Warnings` | `List<Info>` | Yes | | Build warnings |
| `Events` | `EntityEvents` | No | | Entity events (`[JsonIgnore]`) |
| `ParameterSchema` | `System.Type?` | No | | CLR type for parameter record (`[JsonIgnore]`) |

Note: `Class` is serialized as `"action"` in JSON via `[JsonPropertyName("action")]`.

### Methods (ActionMethods.cs)

```csharp
Task Load(PLangContext context)
Task<Data> RunAsync(App app, PLangContext context, CancellationToken ct = default)
```

**Run sequence:**
1. `Libraries.GetCodeGenerated(Module, ActionName)` — find handler
2. `ICodeGenerated.CodeGeneratedExecuteAsync(Parameters, app, context)`
3. Store result as `%__data__%` on `context.Variables` — available to the next action or caller

---

## Collections

### Goals

`Goals` — wraps dual `ConcurrentDictionary` (by name and by path). Case-insensitive.

```csharp
Goal? Get(string name)                  // Cache only, tries variations (strip .goal, path separators)
Task<Goal?> GetAsync(string name, ...)  // Lazy disk load if not cached
void Add(Goal goal)                     // Registers by name and path
bool Remove(string name)
Task<Data> Run(string name, ...)        // GetAsync + RunAsync
Task<Data> LoadFromFileAsync(...)       // Deserialize .pr, wire step.Goal, Add
Task<Data> LoadFromDirectoryAsync(...)  // Load all .pr files recursively

// Filtered views
IEnumerable<Goal> Public               // Visibility == Public
IEnumerable<Goal> Setup                // IsSetup == true
IEnumerable<Goal> Events               // IsEvent == true
IReadOnlyList<Goal> Value              // All goals as list
int Count
```

### Steps

`Steps : List<Step>` — thin wrapper.

```csharp
List<Step> Value { get; }
Task Load(PLangContext context)         // Load each step
```

### Actions

`Actions : List<Action>` — sequential execution with merge.

```csharp
List<Action> Value { get; }
Task Load(PLangContext context)
Task<Data> RunAsync(App app, PLangContext context, CancellationToken ct)
Task<(string?, IError?)> Summary()     // Render action summary via Scriban template
```

`RunAsync` executes actions sequentially, merging results via `Data.Merge()`. Stops on first failure (fail-fast).

## Execution Flow

```
App.RunGoalAsync(goalName, context)
    │
    ├── Goals.GetAsync(goalName)
    │   └── returns null → Data.Fail(GoalError.NotFound)
    │
    └── Goal.RunAsync(app, context)
        ├── context.Goal = goal
        ├── Before.Run events
        ├── CallStack.Push(frame)
        ├── foreach step in Steps
        │   └── Step.RunAsync(app, context)
        │       ├── context.Step = step
        │       ├── CallStack.RecordStep()
        │       ├── Before.Run events
        │       ├── Actions.RunAsync(app, context)
        │       │   └── foreach action in Actions
        │       │       ├── Libraries.GetCodeGenerated(action.Module, action.ActionName)
        │       │       ├── ICodeGenerated.CodeGeneratedExecuteAsync(params, app, context)
        │       │       └── Store result as %__data__% in Variables
        │       └── After.Run events
        ├── After.Run events
        └── CallStack.Pop()
```

## Relationships

- `Goals` is stored in [App](app.md)
- `Goal` contains `Step` instances, each containing `Action` instances
- `Action` references handlers from [Libraries](modules.md)
- Action execution stores results in [Variables](memory-stack.md)
- Execution is tracked via [CallStack](call-stack.md)
- Entity events fire through [EntityEvents](events.md)
