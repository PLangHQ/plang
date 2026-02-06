using PLang.Runtime2.Context;
using PLang.Runtime2.Core;
using PLang.Runtime2.Errors;
using PLang.Runtime2.Memory;

namespace PLang.Runtime2.Modules;

/// <summary>
/// Base class for Runtime2 modules.
/// Provides common functionality and access to execution context.
/// </summary>
public abstract class BaseModule : IModule
{
    private ModuleContext? _context;

    /// <summary>
    /// The name of this module.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Alternative names/aliases for this module.
    /// </summary>
    public virtual IEnumerable<string> Aliases => Enumerable.Empty<string>();

    /// <summary>
    /// The current module context.
    /// </summary>
    protected ModuleContext ModuleContext => _context ?? throw new InvalidOperationException("Module not initialized");

    /// <summary>
    /// The current execution context.
    /// </summary>
    protected PLangContext Context => ModuleContext.Context;

    /// <summary>
    /// The application context.
    /// </summary>
    protected PLangAppContext AppContext => ModuleContext.AppContext;

    /// <summary>
    /// Memory stack for variable access.
    /// </summary>
    protected MemoryStack MemoryStack => ModuleContext.MemoryStack;

    /// <summary>
    /// The current goal being executed.
    /// </summary>
    protected Goal? Goal => ModuleContext.Goal;

    /// <summary>
    /// The current step being executed.
    /// </summary>
    protected Step? Step => ModuleContext.Step;

    /// <summary>
    /// Cancellation token for the current execution.
    /// </summary>
    protected CancellationToken CancellationToken => ModuleContext.CancellationToken;

    /// <summary>
    /// The engine (for running sub-goals).
    /// </summary>
    protected Engine? Engine => ModuleContext.Engine;

    /// <summary>
    /// Initializes the module with context.
    /// </summary>
    public virtual void Initialize(ModuleContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Executes a method on this module.
    /// Override this to implement module-specific logic.
    /// </summary>
    public abstract Task<GoalResult> ExecuteAsync(string method, object? parameters);

    /// <summary>
    /// Checks if this module can handle a given method.
    /// Default implementation checks against GetMethods().
    /// </summary>
    public virtual bool CanHandle(string method)
    {
        return GetMethods().Any(m => m.Equals(method, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets a list of methods this module supports.
    /// Override to provide the list of supported methods.
    /// </summary>
    public abstract IEnumerable<string> GetMethods();

    /// <summary>
    /// Helper to create a success result.
    /// </summary>
    protected static GoalResult Success() => GoalResult.Ok();

    /// <summary>
    /// Helper to create a success result with a value.
    /// </summary>
    protected static GoalResult Success(object? value) => GoalResult.Ok(value);

    /// <summary>
    /// Helper to create an error result.
    /// </summary>
    protected static GoalResult Error(string message, string key = "Error", int statusCode = 400)
        => GoalResult.Fail(message, key, statusCode);

    /// <summary>
    /// Helper to create an error result from ErrorInfo.
    /// </summary>
    protected static GoalResult Error(ErrorInfo error) => GoalResult.Fail(error);

    /// <summary>
    /// Helper to create a success Task.
    /// </summary>
    protected static Task<GoalResult> SuccessTask() => GoalResult.SuccessTask();

    /// <summary>
    /// Helper to create a success Task with a value.
    /// </summary>
    protected static Task<GoalResult> SuccessTask(object? value) => GoalResult.SuccessTask(value);

    /// <summary>
    /// Helper to create an error Task.
    /// </summary>
    protected static Task<GoalResult> ErrorTask(string message, string key = "Error", int statusCode = 400)
        => GoalResult.ErrorTask(message, key, statusCode);

    /// <summary>
    /// Gets a variable from memory.
    /// </summary>
    protected Data? GetVariable(string name) => MemoryStack.Get(name);

    /// <summary>
    /// Gets a typed variable from memory.
    /// </summary>
    protected T? GetVariable<T>(string name) => MemoryStack.Get<T>(name);

    /// <summary>
    /// Sets a variable in memory.
    /// </summary>
    protected void SetVariable(string name, object? value, TypeInfo? typeInfo = null)
        => MemoryStack.Set(name, value, typeInfo);
}
