using System.Collections.Concurrent;
using System.Reflection;
using PLang.Runtime2.modules;
using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Errors;

namespace PLang.Runtime2.Engine.Modules;

/// <summary>
/// Flat action registry. Owns discovery, registration, and resolution of all actions.
/// Built-in actions are discovered from the PLang assembly at construction.
/// External DLLs add actions via Discover(assembly, namespace).
/// </summary>
public sealed class @this
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ActionEntry>> _modules = new(StringComparer.OrdinalIgnoreCase);

    public @this()
    {
        Discover(typeof(@this).Assembly, "PLang.Runtime2.modules");
    }

    /// <summary>
    /// Discovers [Action]-attributed ICodeGenerated types in an assembly and registers them.
    /// External DLLs call this via library.load.
    /// </summary>
    public int Discover(Assembly assembly, string? baseNamespace = null)
    {
        baseNamespace ??= "PLang.Runtime2.modules";
        int count = 0;

        var actionTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<ActionAttribute>() != null
                      && typeof(ICodeGenerated).IsAssignableFrom(t)
                      && !t.IsAbstract);

        foreach (var type in actionTypes)
        {
            if (type.Namespace == null || !type.Namespace.StartsWith(baseNamespace + "."))
                continue;

            var module = type.Namespace[(baseNamespace.Length + 1)..];
            var attr = type.GetCustomAttribute<ActionAttribute>()!;
            var actionName = attr.Name ?? type.Name.ToLowerInvariant();

            RegisterType(module, actionName, type);
            count++;
        }

        return count;
    }

    /// <summary>
    /// Registers an action type for per-call instantiation (stateless, normal path).
    /// </summary>
    public void RegisterType(string module, string actionName, Type type)
    {
        var actions = _modules.GetOrAdd(module, _ => new ConcurrentDictionary<string, ActionEntry>(StringComparer.OrdinalIgnoreCase));
        actions[actionName] = new ActionEntry(type, null);
    }

    /// <summary>
    /// Registers a shared action instance (stateful — external DLLs, test overrides).
    /// Instance takes priority over type during resolution.
    /// </summary>
    public void Register(string module, string actionName, IAction instance)
    {
        var actions = _modules.GetOrAdd(module, _ => new ConcurrentDictionary<string, ActionEntry>(StringComparer.OrdinalIgnoreCase));
        actions[actionName] = new ActionEntry(null, instance);
    }

    /// <summary>
    /// Resolves an action for execution. Returns (action, null) or (null, error).
    /// </summary>
    public (ICodeGenerated? Action, IError? Error) GetCodeGenerated(
        string module, string actionName, PLangContext context)
    {
        if (!_modules.TryGetValue(module, out var actions) ||
            !actions.TryGetValue(actionName, out var entry))
            return (null, ActionError.NotFound($"Action '{module}.{actionName}'", context));

        var action = entry.Create();
        if (action == null)
            return (null, new ActionError(
                $"Action '{module}.{actionName}' does not implement ICodeGenerated",
                context, "ActionError", 500) { ActionModule = module, ActionName = actionName });

        return (action, null);
    }

    // --- Queries ---

    public bool Contains(string module, string actionName)
        => _modules.TryGetValue(module, out var actions) && actions.ContainsKey(actionName);

    public bool Contains(string module)
        => _modules.ContainsKey(module);

    public IEnumerable<string> Names
        => _modules.Keys;

    public IEnumerable<string> GetActions(string module)
        => _modules.TryGetValue(module, out var actions) ? actions.Keys : Enumerable.Empty<string>();

    public Type? GetActionType(string module, string actionName)
    {
        if (!_modules.TryGetValue(module, out var actions) ||
            !actions.TryGetValue(actionName, out var entry))
            return null;

        return entry.Type ?? entry.Instance?.GetType();
    }

    public int Count => _modules.Values.Sum(a => a.Count);

    /// <summary>
    /// All registered instances (for disposal on engine shutdown).
    /// Type-registered actions are per-call — no disposal tracking needed.
    /// </summary>
    public IEnumerable<IAction> All
        => _modules.Values.SelectMany(a => a.Values)
            .Where(e => e.Instance != null)
            .Select(e => e.Instance!);

    public void Clear() => _modules.Clear();
}

/// <summary>
/// Single registry entry — either a Type (per-call instantiation) or a shared Instance.
/// </summary>
public record ActionEntry(Type? Type, IAction? Instance)
{
    public ICodeGenerated? Create()
    {
        if (Instance is ICodeGenerated cg)
            return cg;

        if (Type != null && typeof(ICodeGenerated).IsAssignableFrom(Type))
            return (ICodeGenerated)Activator.CreateInstance(Type)!;

        return null;
    }
}
