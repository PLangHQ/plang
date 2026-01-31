---
name: plang-runtime
description: Expert guidance for PLang Runtime internals. Use when working on the PLang C# codebase, specifically the runtime execution engine, module system, context management, goal execution, and error handling. Covers Engine, ModuleRegistry, PLangContext, PseudoRuntime, MemoryStack, and the module architecture.
---

# PLang Runtime Architecture

## Overview

The PLang runtime executes compiled `.pr` files (JSON instructions). The key components are:

- **Engine** - Main execution engine, runs goals and steps
- **ModuleRegistry** - Manages available modules per-context
- **PLangContext** - Per-request context with memory, modules, sinks
- **PseudoRuntime** - Executes goals with proper context setup
- **MemoryStack** - Variable storage with scoping
- **BaseProgram** - Base class for all modules

## Key Files

```
PLang/Runtime/
├── Engine.cs              # Main execution engine
├── ModuleRegistry.cs      # Module management with copy-on-write
├── EnginePool.cs          # Engine pooling for webserver
├── PseudoRuntime.cs       # Goal execution helper
└── MemoryStack.cs         # Variable storage

PLang/Interfaces/
├── IEngine.cs             # Engine interface
├── IModuleRegistry.cs     # Module registry interface
├── PLangContext.cs        # Per-request context
└── IPLangContextAccessor.cs # AsyncLocal context accessor

PLang/Modules/
├── BaseProgram.cs         # Base class for all modules
└── */Program.cs           # Individual module implementations
```

## Execution Flow

### Goal Execution

```
Engine.RunGoal()
  → RunGoalInternal()
    → For each step:
      → ProcessPrFile()
        → BaseProgram.Run()
          → RunFunctionInternal()
            → method.FastInvoke()
```

### Step Execution with Error Handling

```csharp
// In RunGoalInternal (Engine.cs:1084-1101)
while (true)
{
    (stepReturnValue, stepError) = await ProcessPrFile(goal, step, stepIndex, context);
    if (stepError == null) break; // Success

    var (shouldRetry, handledError) = await HandleStepErrorFlat(...);
    if (!shouldRetry) break;
    retryCount++;
}
```

## ModuleRegistry

### Purpose
Manages module availability per-context. Supports:
- Enable/disable modules dynamically
- Remove modules (security sandboxing)
- Per-context isolation via copy-on-write cloning

### Key Methods

```csharp
public interface IModuleRegistry
{
    void Register<T>() where T : BaseProgram;
    void Remove(string shortName);
    void Disable(string shortName);
    void Enable(string shortName);
    bool IsEnabled(string shortName);
    (T? Module, IError? Error) Get<T>() where T : BaseProgram;
    (BaseProgram? Module, IError? Error) Get(string shortName);
    IReadOnlyList<string> GetRegisteredModules();
    ModuleRegistry Clone();
}
```

### Copy-on-Write Pattern

```csharp
// Clone() is O(1) - shares collections with parent
public ModuleRegistry Clone()
{
    return new ModuleRegistry(_container, _contextAccessor, _modules, _disabled, _removed)
    {
        _ownsCollections = false  // Will copy on first write
    };
}

// Write operations trigger copy
private void EnsureWritable()
{
    if (_ownsCollections) return;
    _modules = new Dictionary<string, Type>(_modules);
    _disabled = new HashSet<string>(_disabled);
    _removed = new HashSet<string>(_removed);
    _ownsCollections = true;
}
```

### Short Name Extraction

Module types follow pattern: `PLang.Modules.<Name>Module.Program`
- `PLang.Modules.TerminalModule.Program` → `"terminal"`
- `PLang.Modules.HttpModule.Program` → `"http"`

## PLangContext

### Purpose
Per-request context containing:
- MemoryStack (variables)
- ModuleRegistry (cloned per-context)
- CallStack
- UserSink/SystemSink (output)
- HttpContext (for web requests)

### Creation

```csharp
public PLangContext(MemoryStack memoryStack, IEngine engine, ExecutionMode executionMode)
{
    MemoryStack = memoryStack;
    Engine = engine;
    // Clone module registry for per-context isolation
    Modules = engine.CloneDefaultModuleRegistry();
    SystemSink = engine.SystemSink;
    UserSink = engine.UserSink;
    CallStack = new();
}
```

### Context Accessor (AsyncLocal)

```csharp
public class ContextAccessor : IPLangContextAccessor
{
    private static AsyncLocal<PLangContext> _current = new();

    public PLangContext Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}
```

## Engine

### Key Properties

```csharp
public interface IEngine
{
    IModuleRegistry Modules { get; }        // Returns context's registry
    IOutputSink UserSink { get; set; }      // User output
    IOutputSink SystemSink { get; set; }    // System output
    IEventRuntime GetEventRuntime();
    MemoryStack GetMemoryStack();

    void Init(IServiceContainer container);
    Task<(object?, IError?)> RunGoal(Goal goal, PLangContext context, uint delayMs = 0);
    IModuleRegistry CloneDefaultModuleRegistry();
}
```

### Engine Pooling (Webserver)

```csharp
// EnginePool.cs - reuses engines for performance
public class EnginePool
{
    public async Task<IEngine> RentAsync(GoalStep? callingStep = null);
    public void Return(IEngine engine);
}
```

## Module Architecture

### BaseProgram

All modules inherit from `BaseProgram`:

```csharp
public abstract class BaseProgram
{
    protected IServiceContainer container;
    protected Goal goal;
    protected GoalStep goalStep;
    protected IPLangContextAccessor contextAccessor;
    protected PLangContext context => contextAccessor.Current;
    protected MemoryStack memoryStack => context.MemoryStack;
    protected IEngine engine;

    public void Init(IServiceContainer container, Goal goal, GoalStep step,
                     Instruction instruction, IPLangContextAccessor contextAccessor);

    public abstract Task<(object?, IError?)> Run();

    // Module-to-module access
    protected (T? Module, IError? Error) Module<T>() where T : BaseProgram
        => engine.Modules.Get<T>();
}
```

### Module-to-Module Calls

```csharp
// In any module, call another module:
public async Task<IError?> DoSomething()
{
    var (httpModule, error) = Module<HttpModule.Program>();
    if (error != null) return error;

    await httpModule.Get("https://api.example.com");
    return null;
}
```

### RunFunctionInternal

The core method execution in BaseProgram:

```csharp
public async Task<(object?, IError?)> RunFunctionInternal(IGenericFunction function)
{
    MethodInfo method = await methodHelper.GetMethod(this, function);
    (parameterValues, error) = methodHelper.GetParameterValues(method, function);

    Task task = method.FastInvoke(this, parameterValues.Values.ToArray()) as Task;

    if (goalStep.WaitForExecution)
        await task;

    // Handle errors, return values, etc.
}
```

## Error Handling

### Error Flow

1. Module method returns `IError` or throws exception
2. `RunFunctionInternal` catches and converts to `IError`
3. `ProcessPrFile` returns error to `RunGoalInternal`
4. `HandleStepErrorFlat` decides: retry or propagate
5. `EventRuntime.RunOnErrorStepEvents` fires error events
6. Error handlers can handle/transform errors

### Error Types

```csharp
public interface IError
{
    string Message { get; }
    string Key { get; }
    int StatusCode { get; }
    Goal? Goal { get; }
    GoalStep? Step { get; }
}

// Common implementations:
- Error           // General error
- ProgramError    // Module-specific error
- ExceptionError  // Wrapped exception
- EndGoal         // Special: ends goal execution
```

## PseudoRuntime

### Purpose
Executes goals with proper context setup, handles:
- Goal lookup
- Context cloning for async execution
- Engine renting for isolated execution
- Parameter passing

### Key Method

```csharp
public async Task<(IEngine, object?, IError?)> RunGoal(
    IEngine engine,
    IPLangContextAccessor contextAccessor,
    string relativeAppPath,
    GoalToCallInfo goalToCall,
    Goal? callingGoal = null,
    bool waitForExecution = true,
    // ... other options
)
```

### Async Goal Execution

When `waitForExecution = false`:
```csharp
// Creates new memory stack (deep clone)
var newMemoryStack = MemoryStack.New(runtimeEngine.Container, runtimeEngine);
foreach (var item in memoryStack.GetMemoryStack())
{
    newMemoryStack.Put(new ObjectValue(item.Name, item.Value.DeepClone()));
}

// Runs in background
task = Task.Run(async () =>
{
    var newContext = context.Clone(newMemoryStack, runtimeEngine);
    await runtimeEngine.RunGoal(goalToRun, newContext);
});
```

## Common Patterns

### Getting Module in BaseProgram

```csharp
// Preferred: returns tuple with error
var (module, error) = Module<HttpModule.Program>();
if (error != null) return error;

// Legacy: throws if async constructor needed
var module = GetProgramModule<HttpModule.Program>();
```

### Context Variables

```csharp
// Add variable to current call frame
context.AddVariable(value, variableName: "myVar");

// Get variable
var value = context.GetVariable<string>("myVar");

// Memory stack (persists across call frames)
memoryStack.Put("key", value);
var val = memoryStack.Get("key");
```

### Security: Disable Modules

```csharp
// In plang code:
// - [env] disable module 'terminal'
// - [env] remove module 'code'

// In C#:
context.Modules.Disable("terminal");
context.Modules.Remove("code");
```

## Testing

### Unit Testing Modules

```csharp
// Use NSubstitute to mock dependencies
var mockPrParser = Substitute.For<IPrParser>();
var mockEngine = Substitute.For<IEngine>();

// ModuleRegistry can be tested directly
var registry = new ModuleRegistry(container, contextAccessor);
registry.Register("test", typeof(TestModule));
var (module, error) = registry.Get("test");
```

### Integration Testing

Run plang tests in a test folder:
```plang
TestModuleRegistry
- [env] get modules, write to %modules%
- [env] disable module 'terminal'
- [env] is module 'terminal' enabled, write to %enabled%
```
