# PLang Runtime Internals Reference

## Directory Structure

```
PLang/
├── Runtime/
│   ├── Engine.cs              # Main execution engine (1200+ lines)
│   ├── ModuleRegistry.cs      # Module management with copy-on-write
│   ├── EnginePool.cs          # Engine pooling for concurrent requests
│   ├── PseudoRuntime.cs       # Goal execution helper
│   ├── MemoryStack.cs         # Variable storage with events
│   └── CallStack.cs           # Goal/step call tracking
│
├── Interfaces/
│   ├── IEngine.cs             # Engine contract
│   ├── IModuleRegistry.cs     # Module registry contract
│   ├── PLangContext.cs        # Per-request context (290+ lines)
│   └── IPLangFileSystem.cs    # File system abstraction
│
├── Modules/
│   ├── BaseProgram.cs         # Base class for modules (900+ lines)
│   ├── BaseBuilder.cs         # Base class for builders
│   └── <Name>Module/
│       ├── Program.cs         # Runtime implementation
│       └── Builder.cs         # Build-time implementation (optional)
│
├── Building/
│   ├── Parsers/
│   │   ├── IPrParser.cs       # Parser interface
│   │   └── PrParser.cs        # Parses .pr files
│   └── Model/
│       ├── Goal.cs            # Goal model
│       ├── GoalStep.cs        # Step model
│       └── Instruction.cs     # Compiled instruction
│
├── Events/
│   ├── EventRuntime.cs        # Event system
│   └── Types/                 # Event type definitions
│
├── Container/
│   └── Container.cs           # DI registration (LightInject)
│
└── Errors/
    ├── IError.cs              # Error interface
    ├── Error.cs               # Base error implementation
    └── Runtime/               # Runtime-specific errors
```

## Engine Lifecycle

### Initialization

```csharp
// In Engine.Init()
public void Init(IServiceContainer container)
{
    this.Container = container;
    this.contextAccessor = container.GetInstance<IPLangContextAccessor>();
    this.eventRuntime = container.GetInstance<IEventRuntime>();
    this.prParser = container.GetInstance<IPrParser>();

    // Initialize default module registry
    _defaultModuleRegistry = new ModuleRegistry(container, contextAccessor);
    _defaultModuleRegistry.RegisterAllFromContainer();

    // Initialize default sinks
    _defaultUserSink = container.GetInstance<IOutputSink>("ConsoleUserSink");
    _defaultSystemSink = container.GetInstance<IOutputSink>("ConsoleSystemSink");
}
```

### Property Accessors (Delegate to Context)

```csharp
// Engine properties delegate to current context with fallback
public IModuleRegistry Modules => contextAccessor?.Current?.Modules ?? _defaultModuleRegistry;
public IOutputSink UserSink
{
    get => contextAccessor?.Current?.UserSink ?? _defaultUserSink;
    set { if (contextAccessor?.Current != null) contextAccessor.Current.UserSink = value; }
}
```

### Engine Pooling

```csharp
// EnginePool manages engine instances for webserver
public class EnginePool
{
    private readonly ConcurrentBag<IEngine> _pool = new();
    private readonly IEngine _rootEngine;

    public async Task<IEngine> RentAsync(GoalStep? callingStep = null)
    {
        if (!_pool.TryTake(out var engine))
        {
            engine = CreateEngine();
        }
        return engine;
    }

    public void Return(IEngine engine)
    {
        // Reset context before returning to pool
        _pool.Add(engine);
    }
}
```

## ModuleRegistry Details

### Module Type Resolution

```csharp
// ~48 modules registered, naming convention:
// PLang.Modules.<Name>Module.Program

// Short name extraction (pre-compiled regex)
private static readonly Regex ModuleNameRegex =
    new(@"PLang\.Modules\.(\w+)Module\.(Program|Builder)", RegexOptions.Compiled);

public static string ExtractShortName(Type moduleType)
{
    var match = ModuleNameRegex.Match(moduleType.FullName);
    if (match.Success)
        return match.Groups[1].Value.ToLowerInvariant();
    // Fallback logic...
}
```

### Module Instance Creation

```csharp
private (BaseProgram?, IError?) CreateModuleInstance(Type moduleType)
{
    var context = _contextAccessor.Current;
    var goal = context?.CallStack?.CurrentGoal ?? Goal.NotFound;
    var step = context?.CallStack?.CurrentStep;
    var instruction = step?.Instruction;

    // Get from DI container
    var program = _container.GetInstance(moduleType) as BaseProgram;

    // Initialize with current context
    program.Init(_container, goal, step, instruction, _contextAccessor);

    return (program, null);
}
```

### Copy-on-Write Implementation

```csharp
// Collections shared until mutation
private Dictionary<string, Type> _modules;
private HashSet<string> _disabled;
private HashSet<string> _removed;
private bool _ownsCollections;

public ModuleRegistry Clone()
{
    // O(1) - just share references
    return new ModuleRegistry(_container, _contextAccessor, _modules, _disabled, _removed)
    {
        _ownsCollections = false
    };
}

private void EnsureWritable()
{
    if (_ownsCollections) return;

    // O(n) - only on first write
    _modules = new Dictionary<string, Type>(_modules, StringComparer.OrdinalIgnoreCase);
    _disabled = new HashSet<string>(_disabled, StringComparer.OrdinalIgnoreCase);
    _removed = new HashSet<string>(_removed, StringComparer.OrdinalIgnoreCase);
    _ownsCollections = true;
}

public void Disable(string shortName)
{
    EnsureWritable();  // Copy only if needed
    _disabled.Add(shortName);
}
```

## PLangContext Details

### Context Properties

```csharp
public class PLangContext
{
    // Identity
    public string Id { get; set; }
    public string Identity { get; set; }
    public SignedMessage? SignedMessage { get; set; }

    // Execution state
    public MemoryStack MemoryStack { get; }
    public CallStack CallStack { get; set; }
    public GoalStep? CallingStep { get; set; }
    public RuntimeEvent? Event { get; set; }
    public IError Error { get; set; }

    // Module management (per-context)
    public IModuleRegistry Modules { get; private set; }

    // Output
    public IOutputSink UserSink { get; set; }
    public IOutputSink SystemSink { get; set; }

    // Web context
    public HttpContext? HttpContext { get; set; }
    public Callback? Callback { get; set; }

    // Shared data
    public ConcurrentDictionary<string, object?> Items { get; }
    public ConcurrentDictionary<string, object?> SharedItems { get; set; }

    // Debug/test modes
    public bool IsAsync { get; set; }
    public bool ShowErrorDetails { get; set; }
    public bool DebugMode { get; set; }
    public ExecutionMode ExecutionMode { get; set; }
    public List<MockData> Mocks { get; }
}
```

### Context Cloning (for async goals)

```csharp
internal PLangContext Clone(MemoryStack memoryStack, IEngine runtimeEngine)
{
    var context = new PLangContext(memoryStack, runtimeEngine, ExecutionMode);

    // Copy identity
    context.CallingStep = this.CallingStep;
    context.Identity = this.Identity;
    context.SignedMessage = this.SignedMessage;

    // New call stack
    context.CallStack = new CallStack();

    // Copy items (shallow)
    foreach (var item in this.Items)
        context.Items.TryAdd(item.Key, item.Value);

    // Share SharedItems reference
    context.SharedItems = this.SharedItems;

    // Copy sinks
    context.SystemSink = this.SystemSink;
    context.UserSink = this.UserSink;

    // Clone module registry
    context.Modules = (this.Modules as ModuleRegistry)?.Clone()
        ?? runtimeEngine.CloneDefaultModuleRegistry();

    return context;
}
```

## BaseProgram Details

### Key Fields and Properties

```csharp
public abstract class BaseProgram
{
    // Injected via Init()
    protected IServiceContainer container;
    protected Goal goal;
    protected GoalStep goalStep;
    protected Instruction instruction;
    protected IPLangContextAccessor contextAccessor;

    // Convenience accessors
    protected PLangContext context => contextAccessor.Current;
    protected MemoryStack memoryStack => context.MemoryStack;
    protected IGenericFunction function;

    // Common dependencies (set by derived classes)
    protected ISettings settings;
    protected IEngine engine;
    protected ILogger logger;
    protected IPLangFileSystem fileSystem;
    protected IAppCache appCache;
}
```

### Init Method

```csharp
public void Init(IServiceContainer container, Goal? goal, GoalStep? step,
                 Instruction? instruction, IPLangContextAccessor contextAccessor)
{
    this.container = container;
    this.goal = goal ?? Goal.NotFound;
    this.goalStep = step ?? new GoalStep();
    this.instruction = instruction;
    this.contextAccessor = contextAccessor;
}
```

### Run Method (Entry Point)

```csharp
public virtual async Task<(object? ReturnValue, IError? Error)> Run()
{
    if (goalStep.Instruction == null) return (null, null);
    if (function == null) function = instruction.GetFunction();

    var result = await RunFunctionInternal(function);
    if (result.Error != null)
    {
        context.CallStack.AddError(result.Error);
    }
    return result;
}
```

### RunFunctionInternal (Core Execution)

```csharp
public async Task<(object?, IError?)> RunFunctionInternal(IGenericFunction function)
{
    this.function = function;

    // 1. Get method by name
    MethodInfo method = await methodHelper.GetMethod(this, function);
    if (method == null)
        return (null, new StepError($"Could not load method {function.Name}"));

    // 2. Check cache
    if (await LoadCached(method, function)) return (null, null);

    // 3. Get parameter values from function definition
    (parameterValues, error) = methodHelper.GetParameterValues(method, function);
    if (error != null) return (null, error);

    // 4. Check for mocks
    Task task = null;
    if (context.Mocks.Count > 0)
        task = RunMockIfMatch(goalStep, function, parameterValues);

    // 5. Invoke method
    if (task == null)
        task = method.FastInvoke(this, parameterValues.Values.ToArray()) as Task;

    // 6. Wait if required
    if (goalStep.WaitForExecution)
        await task;

    // 7. Handle result/errors
    if (task.Status == TaskStatus.Faulted)
    {
        // Handle specific exception types...
    }

    // 8. Process return values
    return await HandleReturnValues(task, method);
}
```

## Goal Execution Flow

### RunGoalInternal (Engine.cs)

```csharp
private async Task<(object?, IError?)> RunGoalInternal(Goal goal, PLangContext context,
    uint delayMs = 0, int startStepIndex = 0)
{
    context.CallStack.EnterGoal(goal, context.Event);

    // Before-goal events
    var beforeGoalEvents = await eventRuntime.GetBeforeGoalEvents(goal);
    foreach (var evt in beforeGoalEvents)
    {
        var evtResult = await eventRuntime.ExecuteEvent(evt, goal, null);
        if (evtResult.Error != null) return evtResult;
    }

    // Execute steps
    for (int stepIndex = startStepIndex; stepIndex < goal.GoalSteps.Count; stepIndex++)
    {
        var step = goal.GoalSteps[stepIndex];
        context.CallingStep = step;
        context.CallStack.SetCurrentStep(step, stepIndex);

        // Before-step events
        var beforeStepEvents = await eventRuntime.GetBeforeStepEvents(goal, step);
        foreach (var evt in beforeStepEvents)
        {
            var evtResult = await eventRuntime.ExecuteEvent(evt, goal, step);
            if (evtResult.Error != null) return (null, evtResult.Error);
        }

        // Execute with retry
        int retryCount = 0;
        while (true)
        {
            (stepReturnValue, stepError) = await ProcessPrFile(goal, step, stepIndex, context);

            if (stepError == null) break;

            var (shouldRetry, handledError) = await HandleStepErrorFlat(
                goal, step, stepIndex, stepError, retryCount, context);

            if (!shouldRetry)
            {
                stepError = handledError;
                break;
            }

            retryCount++;
        }

        // Handle errors
        if (stepError != null)
        {
            if (stepError is EndGoal endGoal) { /* handle EndGoal */ }
            if (stepError is not IErrorHandled)
                return (stepReturnValue, stepError);
        }

        // After-step events
        var afterStepEvents = await eventRuntime.GetAfterStepEvents(goal, step);
        foreach (var evt in afterStepEvents)
        {
            var evtResult = await eventRuntime.ExecuteEvent(evt, goal, step);
            if (evtResult.Error != null) return evtResult;
        }
    }

    // After-goal events
    var afterGoalEvents = await eventRuntime.GetAfterGoalEvents(goal);
    foreach (var evt in afterGoalEvents)
    {
        var evtResult = await eventRuntime.ExecuteEvent(evt, goal, null);
        if (evtResult.Error != null) return evtResult;
    }

    return (returnValues, null);
}
```

## PseudoRuntime Details

### RunGoal Method

```csharp
public async Task<(IEngine, object?, IError?)> RunGoal(
    IEngine engine,
    IPLangContextAccessor contextAccessor,
    string relativeAppPath,
    GoalToCallInfo goalToCall,
    Goal? callingGoal = null,
    bool waitForExecution = true,
    long delayWhenNotWaitingInMilliseconds = 50,
    uint waitForXMillisecondsBeforeRunningGoal = 0,
    int indent = 0,
    bool keepMemoryStackOnAsync = false,
    bool isolated = false,
    bool disableOsGoals = false,
    RuntimeEvent? runtimeEvent = null)
{
    // 1. Find goal
    (var goalToRun, var error) = GoalHelper.GetGoal(
        relativeGoalPath, fileSystem.RootDirectory, goalToCall, goals, systemGoals);
    if (error != null) return (engine, null, error);

    // 2. Rent engine if needed (isolated, async, or different app)
    var runtimeEngine = engine;
    if (isolated || !waitForExecution || CreateNewContainer(goalToRun.AbsoluteGoalFolderPath))
    {
        runtimeEngine = await engine.RentAsync(context.CallingStep);
    }

    // 3. Set parameters
    var memoryStack = context.MemoryStack;
    foreach (var param in parameters ?? [])
    {
        memoryStack.Put(param.Key, param.Value, disableEvent: true);
    }

    // 4. Execute
    if (waitForExecution)
    {
        var task = runtimeEngine.RunGoal(goalToRun, context);
        await task;
        return (engine, task.Result.Variables, task.Result.Error);
    }
    else
    {
        // Async: clone memory and context
        var newMemoryStack = MemoryStack.New(runtimeEngine.Container, runtimeEngine);
        foreach (var item in memoryStack.GetMemoryStack())
        {
            newMemoryStack.Put(new ObjectValue(item.Name, item.Value.DeepClone()));
        }

        var task = Task.Run(async () =>
        {
            var newContext = context.Clone(newMemoryStack, runtimeEngine);
            newContext.IsAsync = true;
            contextAccessor.Current = newContext;
            return await runtimeEngine.RunGoal(goalToRun, newContext);
        });

        return (runtimeEngine, task, null);
    }
}
```

## Common Module Patterns

### Module with Dependencies

```csharp
[Description("Description shown to LLM during build")]
public class Program : BaseProgram
{
    private readonly ISettings settings;
    private readonly IPLangFileSystem fileSystem;
    private readonly IEngine engine;

    public Program(ISettings settings, IPLangFileSystem fileSystem, IEngine engine)
    {
        this.settings = settings;
        this.fileSystem = fileSystem;
        this.engine = engine;
    }

    [Description("Method description for LLM")]
    public async Task<string> DoSomething(string input)
    {
        // Implementation
        return result;
    }

    public async Task<IError?> DoSomethingThatMayFail()
    {
        if (somethingWrong)
            return new ProgramError("Something went wrong", goalStep, function);
        return null;
    }
}
```

### Module Calling Another Module

```csharp
public async Task<IError?> ProcessWithHttp()
{
    // Get module via registry
    var (httpModule, error) = Module<HttpModule.Program>();
    if (error != null) return error;

    // Call method
    var response = await httpModule.Get("https://api.example.com/data");

    // Process response...
    return null;
}
```

### Module with Return Value and Error

```csharp
public async Task<(string? Result, IError? Error)> GetData(string id)
{
    if (string.IsNullOrEmpty(id))
        return (null, new ProgramError("ID is required", goalStep, function));

    var data = await FetchData(id);
    return (data, null);
}
```

## Error Handling Patterns

### Common Error Types

```csharp
// General error
new Error("Something went wrong");

// Program/module error with context
new ProgramError("Invalid input", goalStep, function, Key: "InvalidInput");

// Exception wrapper
new ExceptionError(exception, "Additional context");

// Step error
new StepError("Step failed", goalStep, Key: "StepFailed", StatusCode: 500);

// End goal (special - terminates goal execution)
new EndGoal(goal, goalStep, "Ending early");
```

### Error Handler in Goal

```csharp
// In Engine.HandleStepErrorFlat
private async Task<(bool ShouldRetry, IError? Error)> HandleStepErrorFlat(
    Goal goal, GoalStep step, int stepIndex, IError error, int retryCount, PLangContext context)
{
    // Check for error handler on step
    var errorHandler = step.ErrorHandler;
    if (errorHandler == null)
    {
        // Check goal-level error handler
        errorHandler = goal.GoalSteps
            .FirstOrDefault(s => s.ErrorHandler?.Global == true)?.ErrorHandler;
    }

    // Run error events
    var (vars, handledError) = await eventRuntime.RunOnErrorStepEvents(error, goal, step);

    // Check retry limit
    if (HasRetriedToRetryLimit(errorHandler, retryCount))
        return (false, error);

    // Determine if should retry
    bool shouldRetry = errorHandler?.Retry == true;
    return (shouldRetry, handledError);
}
```

## Testing Patterns

### Unit Test Setup

```csharp
public class ModuleRegistryTests
{
    private ServiceContainer _container;
    private IPLangContextAccessor _contextAccessor;

    [Before(Test)]
    public void Setup()
    {
        _container = new ServiceContainer();
        _contextAccessor = new ContextAccessor();
    }

    [Test]
    public async Task Get_ReturnsModule_WhenRegistered()
    {
        var registry = new ModuleRegistry(_container, _contextAccessor);
        registry.Register("test", typeof(TestModule));

        var (module, error) = registry.Get("test");

        await Assert.That(error).IsNull();
        await Assert.That(module).IsNotNull();
    }
}
```

### Mocking IPrParser

```csharp
var mockPrParser = Substitute.For<IPrParser>();
mockPrParser.GetAllGoals().Returns(new List<Goal>
{
    new Goal { GoalName = "TestGoal" }
});
```
