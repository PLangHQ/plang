using PLang.Runtime2.Context;
using PLang.Runtime2.Errors;
using PLang.Runtime2.Memory;
using PLang.Runtime2.Modules;
using PLang.Runtime2.Serialization;
using PLang.Runtime2.Utility;
using System.Text.Json;

namespace PLang.Runtime2.Core;

/// <summary>
/// Main runtime engine for PLang Runtime2.
/// Executes goals and manages the execution lifecycle.
/// </summary>
public sealed class Engine : IAsyncDisposable
{
    private readonly PLangAppContext _appContext;
    private readonly ModuleRegistry _modules;
    private readonly SerializerRegistry _serializers;
    private readonly Goals _goals;
    private bool _disposed;

    private Actor? _system;
    private Actor? _service;
    private Actor? _user;

    /// <summary>
    /// Unique identifier for this engine instance.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Name of this engine.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Root path of the application.
    /// </summary>
    public string RootPath => _appContext.RootPath;

    /// <summary>
    /// The application context.
    /// </summary>
    public PLangAppContext AppContext => _appContext;

    /// <summary>
    /// The module registry.
    /// </summary>
    public ModuleRegistry Modules => _modules;

    /// <summary>
    /// The serializer registry.
    /// </summary>
    public SerializerRegistry Serializers => _serializers;

    /// <summary>
    /// The loaded goals.
    /// </summary>
    public Goals Goals => _goals;

    /// <summary>
    /// Whether debug mode is enabled.
    /// </summary>
    public bool IsDebugMode
    {
        get => _appContext.IsDebugMode;
        set => _appContext.IsDebugMode = value;
    }

    /// <summary>
    /// System actor - highest trust level, for app operator operations.
    /// Created lazily on first access.
    /// </summary>
    public Actor System => _system ??= new Actor("System", TrustLevel.System, this);

    /// <summary>
    /// Service actor - intermediate trust level, for external service operations.
    /// Created lazily on first access.
    /// </summary>
    public Actor Service => _service ??= new Actor("Service", TrustLevel.Service, this);

    /// <summary>
    /// User actor - lowest trust level, for end user operations.
    /// Created lazily on first access.
    /// </summary>
    public Actor User => _user ??= new Actor("User", TrustLevel.User, this);

    public Engine(PLangAppContext appContext, ModuleRegistry? modules = null, SerializerRegistry? serializers = null)
    {
        Id = Guid.NewGuid().ToString("N")[..12];
        Name = "Runtime2";
        _appContext = appContext;
        _modules = modules ?? new ModuleRegistry();
        _serializers = serializers ?? appContext.Serializers;
        _goals = new Goals();
    }

    /// <summary>
    /// Runs a goal by name using the User actor by default.
    /// </summary>
    public async Task<GoalResult> RunGoalAsync(string goalName, PLangContext? context = null, CancellationToken cancellationToken = default)
    {
        var goal = _goals.Get(goalName);
        if (goal == null)
            return GoalResult.Fail(ErrorInfo.NotFound($"Goal '{goalName}'"));

        return await RunGoalAsync(goal, context, cancellationToken);
    }

    /// <summary>
    /// Runs a goal by name using the specified actor.
    /// </summary>
    public Task<GoalResult> RunGoalAsync(string goalName, Actor actor, CancellationToken cancellationToken = default)
    {
        return RunGoalAsync(goalName, actor.Context, cancellationToken);
    }

    /// <summary>
    /// Runs a goal using the specified actor.
    /// </summary>
    public Task<GoalResult> RunGoalAsync(Goal goal, Actor actor, CancellationToken cancellationToken = default)
    {
        return RunGoalAsync(goal, actor.Context, cancellationToken);
    }

    /// <summary>
    /// Runs a goal using the User actor's context by default.
    /// </summary>
    public async Task<GoalResult> RunGoalAsync(Goal goal, PLangContext? context = null, CancellationToken cancellationToken = default)
    {
        context ??= User.Context;
        context.CurrentGoalName = goal.Name;

        // Check for cancellation
        if (cancellationToken.IsCancellationRequested)
            return GoalResult.Fail("Execution cancelled", "Cancelled", 499);

        // Fire BeforeGoal events
        var beforeResult = await FireEventAsync(EventType.BeforeGoal, goal.Name, context);
        if (!beforeResult)
            return beforeResult;

        // Push call frame if tracking is enabled
        context.CallStack?.Push(goal.Name, goal.Path);

        try
        {
            // Execute each step
            for (var i = 0; i < goal.Steps.Count; i++)
            {
                context.CurrentStepIndex = i;
                var step = goal.Steps[i];

                var stepResult = await ExecuteStepAsync(step, context, cancellationToken);
                if (!stepResult)
                {
                    // Handle error
                    if (!(step.OnError?.IgnoreError ?? false))
                    {
                        await FireEventAsync(EventType.OnError, goal.Name, context, stepResult.Error);
                        return stepResult;
                    }
                }

                if (cancellationToken.IsCancellationRequested)
                    return GoalResult.Fail("Execution cancelled", "Cancelled", 499);
            }

            // Fire AfterGoal events
            var afterResult = await FireEventAsync(EventType.AfterGoal, goal.Name, context);
            if (!afterResult)
                return afterResult;

            return GoalResult.Ok();
        }
        finally
        {
            context.CallStack?.Pop();
        }
    }

    /// <summary>
    /// Executes a single step.
    /// </summary>
    public async Task<GoalResult> ExecuteStepAsync(Step step, PLangContext context, CancellationToken cancellationToken = default)
    {
        // Record step execution
        context.CallStack?.RecordStep(step.Index, step.Text);

        // Fire BeforeStep events
        var beforeResult = await FireEventAsync(EventType.BeforeStep, context.CurrentGoalName, context, stepText: step.Text);
        if (!beforeResult)
            return beforeResult;

        try
        {
            GoalResult lastResult = GoalResult.Ok();

            foreach (var action in step.Actions)
            {
                // Get the module
                var module = _modules.Get(action.Class);
                if (module == null)
                    return GoalResult.Fail($"Action '{action.Class}' not found", "ActionNotFound", 404);

                // Initialize module with context
                var moduleContext = new ModuleContext
                {
                    Context = context,
                    Goal = step.Goal,
                    Step = step,
                    Engine = this
                };
                module.Initialize(moduleContext);

                // Convert List<Data> parameters to Dictionary for module consumption
                object? moduleParams = action.Parameters.Count > 0
                    ? action.Parameters.ToDictionary(d => d.Name, d => d.Value) as IDictionary<string, object?>
                    : null;

                // Execute the method
                var result = await module.ExecuteAsync(action.Method, moduleParams);

                if (result.Success)
                {
                    // Write result to action's return variables
                    if (action.Return.Variables != null)
                    {
                        foreach (var returnVar in action.Return.Variables)
                        {
                            context.MemoryStack.Set(returnVar.Name, result.Value);
                        }
                    }
                }
                else
                {
                    action.Return.Error = result.Error;
                    return result;
                }

                lastResult = result;
            }

            // Fire AfterStep events
            var afterResult = await FireEventAsync(EventType.AfterStep, context.CurrentGoalName, context, stepText: step.Text);
            if (!afterResult)
                return afterResult;

            return lastResult;
        }
        catch (Exception ex)
        {
            var error = ErrorInfo.FromException(ex);
            context.CallStack?.AddError(error);
            return GoalResult.Fail(error);
        }
    }

    /// <summary>
    /// Loads a goal from a .pr file.
    /// </summary>
    public async Task<GoalResult> LoadGoalFromFileAsync(string prFilePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await File.ReadAllTextAsync(prFilePath, cancellationToken);
            var data = JsonSerializer.Deserialize<GoalData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (data == null)
                return GoalResult.Fail($"Failed to parse goal file: {prFilePath}");

            var goal = GoalDataConverter.ToGoal(data, prPath: prFilePath);
            _goals.Add(goal);

            return GoalResult.Ok(goal);
        }
        catch (Exception ex)
        {
            return GoalResult.Fail(ErrorInfo.FromException(ex));
        }
    }

    /// <summary>
    /// Loads all goals from a directory.
    /// </summary>
    public async Task<GoalResult> LoadGoalsFromDirectoryAsync(string directory, string pattern = "*.pr.json", CancellationToken cancellationToken = default)
    {
        try
        {
            var files = Directory.GetFiles(directory, pattern, SearchOption.AllDirectories);
            var loadedCount = 0;

            foreach (var file in files)
            {
                var result = await LoadGoalFromFileAsync(file, cancellationToken);
                if (result)
                    loadedCount++;
            }

            return GoalResult.Ok(loadedCount);
        }
        catch (Exception ex)
        {
            return GoalResult.Fail(ErrorInfo.FromException(ex));
        }
    }

    /// <summary>
    /// Creates a new execution context.
    /// </summary>
    public PLangContext CreateContext(MemoryStack? memoryStack = null)
    {
        var context = new PLangContext(_appContext, memoryStack)
        {
            CallStack = new CallStack()
        };
        return context;
    }

    /// <summary>
    /// Fires an event.
    /// </summary>
    private async Task<GoalResult> FireEventAsync(
        EventType eventType,
        string? goalName,
        PLangContext context,
        ErrorInfo? error = null,
        string? stepText = null)
    {
        var eventContext = new EventContext
        {
            EventType = eventType,
            GoalName = goalName,
            StepIndex = context.CurrentStepIndex,
            StepText = stepText,
            Error = error
        };

        return await _appContext.Events.DispatchAsync(eventContext);
    }

    /// <summary>
    /// Registers built-in modules.
    /// </summary>
    public void RegisterBuiltInModules()
    {
        _modules.Register(new VariableModule());
        _modules.Register(new OutputModule());
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Dispose created actors
        if (_system != null)
            await _system.DisposeAsync();
        if (_service != null)
            await _service.DisposeAsync();
        if (_user != null)
            await _user.DisposeAsync();

        // Dispose any disposable modules
        foreach (var module in _modules.All)
        {
            if (module is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
            else if (module is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
