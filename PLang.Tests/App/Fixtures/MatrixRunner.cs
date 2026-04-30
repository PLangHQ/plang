using System.Reflection;
using global::App.modules;

namespace PLang.Tests.App.Fixtures;

/// <summary>
/// Runs a matrix handler through the production execution path so generated property
/// resolution, marker wiring, provider injection, and (post-Phase-3) App.Run scaffolding
/// are all exercised. Tests construct an Action with synthetic Parameters/Defaults,
/// optionally seed Variables, and read back the result Data plus diagnostic context.
///
/// Matrix handlers live in App.modules.matrix.* — not auto-registered by Modules.Discover
/// (which walks PLang.dll only). RunAsync registers the handler type on demand using
/// its action attribute name and dispatches via App.Run.
/// </summary>
public static class MatrixRunner
{
    /// <summary>
    /// Result envelope for matrix tests — exposes the result Data plus the per-property
    /// snapshot the handler attached to errors. Snapshot is null on success.
    /// </summary>
    public sealed record Result(
        Data Data,
        IReadOnlyList<global::App.Errors.ParamSnapshot>? Snapshot);

    /// <summary>
    /// Runs the matrix handler TAction through App.Run. Parameters and defaults are
    /// supplied as (name, value) pairs; variables are seeded into context.Variables.
    /// </summary>
    public static async Task<Result> RunAsync<TAction>(
        global::App.@this app,
        IEnumerable<(string name, object? value)>? parameters = null,
        IEnumerable<(string name, object? value)>? defaults = null,
        IDictionary<string, object?>? variables = null,
        Step? step = null)
        where TAction : class, ICodeGenerated
    {
        EnsureRegistered<TAction>(app);

        var (module, actionName) = ModuleAndAction<TAction>();
        var action = new PrAction
        {
            Module = module,
            ActionName = actionName,
            Parameters = (parameters ?? Array.Empty<(string, object?)>())
                .Select(p => new Data(p.name, p.value)).ToList(),
            Defaults = defaults?.Select(d => new Data(d.name, d.value)).ToList(),
            Step = step
        };

        var context = app.Context;
        if (variables != null)
        {
            foreach (var kv in variables)
                context.Variables.Set(kv.Key, kv.Value);
        }

        var data = await app.Run(action, context);
        var snapshot = (data.Error as global::App.Errors.Error)?.Params;
        return new Result(data, snapshot);
    }

    /// <summary>
    /// Construct a handler instance directly (no Modules lookup). Properties are
    /// supplied via the action's Parameters list, exactly as App.Run would do — but
    /// without the App.Run scaffolding wrap. Useful for tests that want to inspect
    /// generated property behaviour in isolation.
    /// </summary>
    public static async Task<Result> RunDirectAsync<TAction>(
        global::App.@this app,
        IEnumerable<(string name, object? value)>? parameters = null,
        IEnumerable<(string name, object? value)>? defaults = null,
        IDictionary<string, object?>? variables = null,
        Step? step = null)
        where TAction : class, ICodeGenerated, new()
    {
        var (module, actionName) = ModuleAndAction<TAction>();
        var action = new PrAction
        {
            Module = module,
            ActionName = actionName,
            Parameters = (parameters ?? Array.Empty<(string, object?)>())
                .Select(p => new Data(p.name, p.value)).ToList(),
            Defaults = defaults?.Select(d => new Data(d.name, d.value)).ToList(),
            Step = step
        };

        var context = app.Context;
        if (variables != null)
        {
            foreach (var kv in variables)
                context.Variables.Set(kv.Key, kv.Value);
        }

        var handler = new TAction();
        var data = await handler.ExecuteAsync(action, context);
        var snapshot = (data.Error as global::App.Errors.Error)?.Params;
        return new Result(data, snapshot);
    }

    /// <summary>
    /// Registers a single matrix handler type by reading its [Action] attribute.
    /// Idempotent — second call replaces the first.
    /// </summary>
    public static void EnsureRegistered<TAction>(global::App.@this app)
        where TAction : class, ICodeGenerated
    {
        var (module, actionName) = ModuleAndAction<TAction>();
        if (app.Modules.Contains(module, actionName)) return;
        app.Modules.RegisterType(module, actionName, typeof(TAction));
    }

    /// <summary>
    /// Bulk-register every matrix handler in the test assembly so ad-hoc resolution by
    /// (module, name) works. Useful for tests that drive multiple handlers in one App.
    /// </summary>
    public static void RegisterAllMatrixHandlers(global::App.@this app)
    {
        var asm = typeof(MatrixRunner).Assembly;
        var matrixTypes = asm.GetTypes().Where(t =>
            t.GetCustomAttribute<ActionAttribute>() != null
            && typeof(ICodeGenerated).IsAssignableFrom(t)
            && !t.IsAbstract
            && (t.Namespace?.StartsWith("App.modules.matrix.") ?? false));

        foreach (var type in matrixTypes)
        {
            var attr = type.GetCustomAttribute<ActionAttribute>()!;
            var actionName = attr.Name ?? type.Name.ToLowerInvariant();
            var moduleNs = type.Namespace!;
            var module = moduleNs.Substring("App.modules.".Length);
            if (!app.Modules.Contains(module, actionName))
                app.Modules.RegisterType(module, actionName, type);
        }
    }

    private static (string module, string actionName) ModuleAndAction<TAction>()
    {
        var t = typeof(TAction);
        var attr = t.GetCustomAttribute<ActionAttribute>()
            ?? throw new InvalidOperationException($"{t.FullName} is missing [Action]");
        var actionName = attr.Name ?? t.Name.ToLowerInvariant();
        var moduleNs = t.Namespace
            ?? throw new InvalidOperationException($"{t.FullName} has no namespace");
        var module = moduleNs.StartsWith("App.modules.")
            ? moduleNs.Substring("App.modules.".Length)
            : moduleNs;
        return (module, actionName);
    }
}
