using System.Collections.Concurrent;
using System.Reflection;
using PLang.Runtime2.actions;

namespace PLang.Runtime2.Engine.Libraries;

/// <summary>
/// A single library — one assembly's worth of action handlers.
/// Owns handler registration, discovery, and lookup scoped to this library.
/// </summary>
public sealed class Library
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, IClass>> _handlers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Type>> _actionTypes = new(StringComparer.OrdinalIgnoreCase);

    public string Name { get; }
    public Assembly? Assembly { get; }

    public Library(string name, Assembly? assembly = null)
    {
        Name = name;
        Assembly = assembly;
    }

    /// <summary>
    /// Discovers [Action]-attributed types in the library's assembly and registers them.
    /// </summary>
    public void Discover(string? baseNamespace = null)
    {
        if (Assembly == null) return;

        baseNamespace ??= "PLang.Runtime2.actions";

        var actionAttrTypes = Assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<ActionAttribute>() != null
                      && typeof(ICodeGenerated).IsAssignableFrom(t)
                      && !t.IsAbstract);

        foreach (var type in actionAttrTypes)
        {
            if (type.Namespace == null || !type.Namespace.StartsWith(baseNamespace + "."))
                continue;

            var module = type.Namespace[(baseNamespace.Length + 1)..];
            var attr = type.GetCustomAttribute<ActionAttribute>()!;
            var actionName = attr.Name ?? type.Name.ToLowerInvariant();

            RegisterCodeGenerated(module, actionName, type);
        }
    }

    /// <summary>
    /// Registers a specific handler instance. Used by tests and custom modules.
    /// WARNING: Instance is shared — only use when you need to track state across calls (e.g., test counters).
    /// </summary>
    public void Register(string module, string actionName, IClass handler)
    {
        var actions = _handlers.GetOrAdd(module, _ => new ConcurrentDictionary<string, IClass>(StringComparer.OrdinalIgnoreCase));
        actions[actionName] = handler;
    }

    /// <summary>
    /// Registers a handler Type for per-call instantiation (thread-safe).
    /// </summary>
    public void RegisterCodeGenerated(string module, string actionName, Type type)
    {
        var actions = _actionTypes.GetOrAdd(module, _ => new ConcurrentDictionary<string, Type>(StringComparer.OrdinalIgnoreCase));
        actions[actionName] = type;
    }

    /// <summary>
    /// Gets an explicitly registered handler instance.
    /// </summary>
    public IClass? Get(string module, string actionName)
    {
        if (string.IsNullOrEmpty(module) || string.IsNullOrEmpty(actionName))
            return null;

        if (_handlers.TryGetValue(module, out var actions) &&
            actions.TryGetValue(actionName, out var handler))
            return handler;

        return null;
    }

    /// <summary>
    /// Gets a handler for execution. Returns ICodeGenerated or null.
    /// Explicit instances checked first, then per-call type instantiation.
    /// </summary>
    public ICodeGenerated? GetCodeGenerated(string module, string actionName)
    {
        // Check explicit instances first
        var handler = Get(module, actionName);
        if (handler != null)
            return handler as ICodeGenerated;

        // Per-call instantiation from registered Types
        if (_actionTypes.TryGetValue(module, out var actionTypes) &&
            actionTypes.TryGetValue(actionName, out var actionType))
        {
            if (!typeof(ICodeGenerated).IsAssignableFrom(actionType))
                return null;

            return (ICodeGenerated)Activator.CreateInstance(actionType)!;
        }

        return null;
    }

    public bool Contains(string module, string actionName)
    {
        if (Get(module, actionName) != null)
            return true;

        return _actionTypes.TryGetValue(module, out var actionTypes) &&
               actionTypes.ContainsKey(actionName);
    }

    public bool Contains(string module)
    {
        return _handlers.ContainsKey(module) || _actionTypes.ContainsKey(module);
    }

    /// <summary>
    /// Gets the CLR type for a handler, checking both explicit and type-registered handlers.
    /// </summary>
    public Type? GetActionType(string module, string actionName)
    {
        var handler = Get(module, actionName);
        if (handler != null)
            return handler.GetType();

        if (_actionTypes.TryGetValue(module, out var actionTypes) &&
            actionTypes.TryGetValue(actionName, out var type))
            return type;

        return null;
    }

    public IEnumerable<string> GetActions(string module)
    {
        var classActions = _handlers.TryGetValue(module, out var actions)
            ? actions.Keys : Enumerable.Empty<string>();
        var actionTypeActions = _actionTypes.TryGetValue(module, out var actionTypes)
            ? actionTypes.Keys : Enumerable.Empty<string>();
        return classActions.Concat(actionTypeActions).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    public IEnumerable<string> Modules =>
        _handlers.Keys.Concat(_actionTypes.Keys).Distinct(StringComparer.OrdinalIgnoreCase);

    public int Count =>
        _handlers.Values.Sum(c => c.Count) + _actionTypes.Values.Sum(c => c.Count);

    /// <summary>
    /// Yields all explicitly registered handler instances (for disposal on engine shutdown).
    /// Type-registered handlers are per-call and need no disposal tracking.
    /// </summary>
    public IEnumerable<IClass> All
    {
        get
        {
            foreach (var actions in _handlers.Values)
                foreach (var handler in actions.Values)
                    yield return handler;
        }
    }

    public void Clear()
    {
        _handlers.Clear();
        _actionTypes.Clear();
    }
}
