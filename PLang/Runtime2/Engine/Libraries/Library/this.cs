using System.Collections.Concurrent;
using System.Reflection;
using PLang.Runtime2.modules;

namespace PLang.Runtime2.Engine.Libraries.Library;

/// <summary>
/// A single library — one assembly's worth of actions.
/// Owns action registration, discovery, and lookup scoped to this library.
/// </summary>
public sealed class @this
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, IAction>> _actions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Type>> _actionTypes = new(StringComparer.OrdinalIgnoreCase);

    public string Name { get; }
    public Assembly? Assembly { get; }

    public @this(string name, Assembly? assembly = null)
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

        baseNamespace ??= "PLang.Runtime2.modules";

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
    /// Registers a specific action instance. Used by tests and custom modules.
    /// WARNING: Instance is shared — only use when you need to track state across calls (e.g., test counters).
    /// </summary>
    public void Register(string module, string actionName, IAction action)
    {
        var actions = _actions.GetOrAdd(module, _ => new ConcurrentDictionary<string, IAction>(StringComparer.OrdinalIgnoreCase));
        actions[actionName] = action;
    }

    /// <summary>
    /// Registers an action Type for per-call instantiation (thread-safe).
    /// </summary>
    public void RegisterCodeGenerated(string module, string actionName, Type type)
    {
        var actions = _actionTypes.GetOrAdd(module, _ => new ConcurrentDictionary<string, Type>(StringComparer.OrdinalIgnoreCase));
        actions[actionName] = type;
    }

    /// <summary>
    /// Gets an explicitly registered action instance.
    /// </summary>
    public IAction? Get(string module, string actionName)
    {
        if (string.IsNullOrEmpty(module) || string.IsNullOrEmpty(actionName))
            return null;

        if (_actions.TryGetValue(module, out var actions) &&
            actions.TryGetValue(actionName, out var action))
            return action;

        return null;
    }

    /// <summary>
    /// Gets an action for execution. Returns ICodeGenerated or null.
    /// Explicit instances checked first, then per-call type instantiation.
    /// </summary>
    public ICodeGenerated? GetCodeGenerated(string module, string actionName)
    {
        // Check explicit instances first
        var action = Get(module, actionName);
        if (action != null)
            return action as ICodeGenerated;

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
        return _actions.ContainsKey(module) || _actionTypes.ContainsKey(module);
    }

    /// <summary>
    /// Gets the CLR type for an action, checking both explicit and type-registered actions.
    /// </summary>
    public Type? GetActionType(string module, string actionName)
    {
        var action = Get(module, actionName);
        if (action != null)
            return action.GetType();

        if (_actionTypes.TryGetValue(module, out var actionTypes) &&
            actionTypes.TryGetValue(actionName, out var type))
            return type;

        return null;
    }

    public IEnumerable<string> GetActions(string module)
    {
        var classActions = _actions.TryGetValue(module, out var actions)
            ? actions.Keys : Enumerable.Empty<string>();
        var actionTypeActions = _actionTypes.TryGetValue(module, out var actionTypes)
            ? actionTypes.Keys : Enumerable.Empty<string>();
        return classActions.Concat(actionTypeActions).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    public IEnumerable<string> Modules =>
        _actions.Keys.Concat(_actionTypes.Keys).Distinct(StringComparer.OrdinalIgnoreCase);

    public int Count =>
        _actions.Values.Sum(c => c.Count) + _actionTypes.Values.Sum(c => c.Count);

    /// <summary>
    /// Yields all explicitly registered action instances (for disposal on engine shutdown).
    /// Type-registered actions are per-call and need no disposal tracking.
    /// </summary>
    public IEnumerable<IAction> All
    {
        get
        {
            foreach (var actions in _actions.Values)
                foreach (var action in actions.Values)
                    yield return action;
        }
    }

    public void Clear()
    {
        _actions.Clear();
        _actionTypes.Clear();
    }
}
