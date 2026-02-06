# Goals, Steps & Execution

Goals are the primary execution units in PLang. Each goal contains a sequence of steps that execute in order.

## Goals Collection

Manages the collection of goals loaded into the engine.

### API Surface

```csharp
public sealed class Goals
{
    // Properties
    public int Count { get; }
    public IEnumerable<string> Names { get; }
    public IEnumerable<Goal> All { get; }
    public IEnumerable<Goal> Public { get; }
    public IEnumerable<Goal> Setup { get; }
    public IEnumerable<Goal> Events { get; }

    // Indexer
    public Goal? this[string name] { get; }

    // Methods
    public void Add(Goal goal)
    public Goal? Get(string name)
    public bool Contains(string name)
    public bool Remove(string name)
    public void Clear()
}
```

### Behavior & Rules

- Goal lookup is case-insensitive
- `Get(name)` tries multiple variations:
  - Exact name
  - With `.goal` extension
  - With leading slash trimmed
  - With backslashes converted to forward slashes
- `Add` registers goal by name, `RelativePath`, and `FilePath`
- Adding a goal with the same name replaces the existing one
- `Public` returns goals with `Visibility == GoalVisibility.Public`
- `Setup` returns goals with `IsSetup == true`
- `Events` returns goals with `IsEvent == true`

### Code Examples

```csharp
var goals = new Goals();

// Add goals
goals.Add(new Goal { Name = "CreateUser" });
goals.Add(new Goal { Name = "DeleteUser", RelativePath = "users/delete" });

// Lookup
var goal = goals.Get("CreateUser");       // by name
var goal2 = goals.Get("users/delete");     // by relative path
var goal3 = goals["CreateUser"];           // via indexer

// Check existence
if (goals.Contains("CreateUser"))
{
    // goal exists
}

// Iterate
foreach (var g in goals.All)
{
    Console.WriteLine(g.Name);
}
```

## Goal Class

Represents a single goal — a named unit of execution containing steps.

### API Surface

```csharp
public enum GoalVisibility
{
    Private,
    Public
}

public partial class Goal
{
    // Identity
    public string Name { get; set; }
    public string? Description { get; set; }
    public string? Comment { get; set; }
    public string? Hash { get; set; }

    // Paths
    public string? FilePath { get; set; }
    public string? PrFilePath { get; set; }
    public string? RelativePath { get; set; }

    // Configuration
    public GoalVisibility Visibility { get; set; }
    public bool IsSetup { get; set; }
    public bool IsEvent { get; set; }
    public Dictionary<string, string>? InputParameters { get; set; }

    // Execution
    public List<Step> Steps { get; set; }
    public List<string> SubGoals { get; set; }

    // Methods
    public string ToText()
    public string FullPath { get; }
}
```

### Behavior & Rules

- `Name` — the goal identifier used for lookup
- `Visibility` — `Private` (default) or `Public`
- `IsSetup` — if true, goal runs during application initialization
- `IsEvent` — if true, goal is an event handler
- `InputParameters` — expected parameters as `name → type` mapping
- `Steps` — ordered list of steps to execute
- `SubGoals` — names of sub-goals referenced by this goal
- `FullPath` — computed from `RelativePath` or `Name`
- `ToText()` — returns human-readable representation

### Code Example

```csharp
var goal = new Goal
{
    Name = "CreateUser",
    Description = "Creates a new user account",
    Visibility = GoalVisibility.Public,
    InputParameters = new Dictionary<string, string>
    {
        ["name"] = "string",
        ["email"] = "string"
    },
    Steps = new List<Step>
    {
        new Step { Index = 0, Text = "validate input", ModuleName = "validation", MethodName = "validate" },
        new Step { Index = 1, Text = "insert user", ModuleName = "db", MethodName = "insert" }
    }
};
```

## Step Class

Represents a single step within a goal.

### API Surface

```csharp
public partial class Step
{
    // Identity
    public int Index { get; set; }
    public string Text { get; set; }
    public int LineNumber { get; set; }
    public int Indent { get; set; }
    public string? Comment { get; set; }

    // Execution
    public string ModuleName { get; set; }
    public string MethodName { get; set; }
    public object? Parameters { get; set; }
    public string? ReturnVariable { get; set; }

    // Error handling
    public bool CatchError { get; set; }
    public string? OnErrorGoal { get; set; }
    public bool WaitForExecution { get; set; }

    // Reference
    public Goal? Goal { get; set; }

    // Methods
    public Step Clone()
}
```

### Behavior & Rules

- `Index` — zero-based position in the goal's step list
- `Text` — original PLang natural language text
- `ModuleName` — which module handles this step
- `MethodName` — which method on the module
- `Parameters` — method parameters (typically `Dictionary<string, object?>`)
- `ReturnVariable` — if set, stores result in this variable name
- `CatchError` — if true, continue execution on error
- `OnErrorGoal` — goal to run if step fails
- `WaitForExecution` — if true (default), wait for step completion
- `Goal` — back-reference to parent goal (set after loading)
- `Clone()` — creates a shallow copy

### Code Example

```csharp
var step = new Step
{
    Index = 0,
    Text = "insert into users, name=%name%, email=%email%, write to %user%",
    LineNumber = 2,
    ModuleName = "db",
    MethodName = "insert",
    Parameters = new Dictionary<string, object?>
    {
        ["table"] = "users",
        ["columns"] = new { name = "%name%", email = "%email%" }
    },
    ReturnVariable = "user",
    CatchError = false
};
```

## Execution Flow

```
Engine.RunGoalAsync(goalName, context)
    │
    ├── goals.Get(goalName)
    │   └── returns null → GoalResult.Fail("NotFound")
    │
    ├── context.CurrentGoalName = goal.Name
    │
    ├── context.CallStack?.Push(goal.Name)
    │
    ├── appContext.Events.DispatchAsync(EventType.BeforeGoal, context)
    │
    ├── foreach step in goal.Steps
    │   │
    │   └── Engine.ExecuteStepAsync(step, context)
    │       │
    │       ├── modules.Get(step.ModuleName)
    │       │   └── returns null → GoalResult.Fail("ModuleNotFound")
    │       │
    │       ├── module.Initialize(moduleContext)
    │       │
    │       ├── callStack?.Current?.RecordStep(step.Index, step.Text)
    │       │
    │       ├── module.ExecuteAsync(step.MethodName, step.Parameters)
    │       │   └── exception → GoalResult.Fail(exception)
    │       │
    │       ├── if step.ReturnVariable && result.Success
    │       │   └── memoryStack.Set(step.ReturnVariable, result.Value)
    │       │
    │       └── if !result.Success && !step.CatchError
    │           └── return result (stop execution)
    │
    ├── appContext.Events.DispatchAsync(EventType.AfterGoal, context)
    │
    ├── context.CallStack?.Pop()
    │
    └── return GoalResult.Ok(lastResult)
```

## PLang Usage

```plang
CreateUser
- validate %name% is not empty
- validate %email% is valid email
- insert into users, name=%name%, email=%email%, write to %user%
- return %user%
```

This compiles to a Goal with four Steps, each mapped to a module and method.

## Relationships

- `Goals` is stored in [Engine](engine.md)
- `Goal` contains `Step` instances
- `Step` references modules from [ModuleRegistry](modules.md)
- Step execution stores results in [MemoryStack](memory-stack.md)
- Execution is tracked via [CallStack](call-stack.md)
- Events fire through [EventCollection](events.md)
