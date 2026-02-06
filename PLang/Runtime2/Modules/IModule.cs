using PLang.Runtime2.Context;
using PLang.Runtime2.Core;
using PLang.Runtime2.Memory;

namespace PLang.Runtime2.Modules;

/// <summary>
/// Interface for Runtime2 modules.
/// Modules handle specific types of operations (http, db, file, etc.).
/// </summary>
public interface IModule
{
    /// <summary>
    /// The name of this module (e.g., "http", "db", "variable").
    /// Used for routing steps to the correct module.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Alternative names/aliases for this module.
    /// </summary>
    IEnumerable<string> Aliases => Enumerable.Empty<string>();

    /// <summary>
    /// Initializes the module with context.
    /// </summary>
    void Initialize(ModuleContext context);

    /// <summary>
    /// Executes a method on this module.
    /// </summary>
    /// <param name="method">The method name to execute.</param>
    /// <param name="parameters">The parameters for the method (typed request object).</param>
    /// <returns>A GoalResult indicating success/failure and any return value.</returns>
    Task<GoalResult> ExecuteAsync(string method, object? parameters);

    /// <summary>
    /// Checks if this module can handle a given method.
    /// </summary>
    bool CanHandle(string method);

    /// <summary>
    /// Gets a list of methods this module supports.
    /// </summary>
    IEnumerable<string> GetMethods();
}

/// <summary>
/// Context provided to modules during initialization and execution.
/// </summary>
public sealed class ModuleContext
{
    /// <summary>
    /// The current execution context.
    /// </summary>
    public PLangContext Context { get; init; } = null!;

    /// <summary>
    /// The application context.
    /// </summary>
    public PLangAppContext AppContext => Context.AppContext;

    /// <summary>
    /// Memory stack for variable access.
    /// </summary>
    public MemoryStack MemoryStack => Context.MemoryStack;

    /// <summary>
    /// The current goal being executed.
    /// </summary>
    public Goal? Goal { get; init; }

    /// <summary>
    /// The current step being executed.
    /// </summary>
    public Step? Step { get; init; }

    /// <summary>
    /// Cancellation token for the current execution.
    /// </summary>
    public CancellationToken CancellationToken => Context.CancellationToken;

    /// <summary>
    /// Reference to the engine (for running sub-goals).
    /// </summary>
    public Engine? Engine { get; init; }
}
