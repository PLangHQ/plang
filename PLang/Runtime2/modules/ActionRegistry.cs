using System.Collections.Concurrent;
using System.Reflection;

namespace PLang.Runtime2.modules;

public sealed class ActionRegistry
{
    // Explicit instances (Register(instance) for tests/custom handlers)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, IClass>> _handlers = new(StringComparer.OrdinalIgnoreCase);

    // Types for per-call instantiation (thread-safe: new instance per call)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Type>> _actionTypes = new(StringComparer.OrdinalIgnoreCase);

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

    public void DiscoverAndRegister(Assembly assembly)
    {
        const string baseNs = "PLang.Runtime2.modules";

        // [Action] attribute-based classes → stored as Type for per-call instantiation
        var actionAttrTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<ActionAttribute>() != null
                      && typeof(ICodeGenerated).IsAssignableFrom(t)
                      && !t.IsAbstract);

        foreach (var type in actionAttrTypes)
        {
            if (type.Namespace == null || !type.Namespace.StartsWith(baseNs + "."))
                continue;

            var module = type.Namespace[(baseNs.Length + 1)..];
            var attr = type.GetCustomAttribute<ActionAttribute>()!;
            var actionName = attr.Name ?? type.Name.ToLowerInvariant();

            RegisterCodeGenerated(module, actionName, type);
        }
    }

    /// <summary>
    /// Gets an explicitly registered handler instance (for tests/custom).
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
    /// Gets a handler for execution. Creates new instances for Type-registered handlers (thread-safe).
    /// Explicit Register(instance) handlers are returned as-is (for tests).
    /// </summary>
    public (ICodeGenerated? Handler, Errors.IError? Error) GetCodeGenerated(string module, string actionName, Context.PLangContext context)
    {
        // Check explicit instances first (Register(instance) overrides discovered types)
        var handler = Get(module, actionName);
        if (handler != null)
        {
            if (handler is not ICodeGenerated codeGenerated)
                return (null, new Errors.ActionError($"Handler '{module}.{actionName}' does not implement ICodeGenerated", context, "HandlerError", 500) { ActionModule = module, ActionName = actionName });
            return (codeGenerated, null);
        }

        // Per-call instantiation from registered Types (both IClass and [Action] paths)
        if (_actionTypes.TryGetValue(module, out var actionTypes) &&
            actionTypes.TryGetValue(actionName, out var actionType))
        {
            if (!typeof(ICodeGenerated).IsAssignableFrom(actionType))
                return (null, new Errors.ActionError($"Handler '{module}.{actionName}' does not implement ICodeGenerated", context, "HandlerError", 500) { ActionModule = module, ActionName = actionName });

            var instance = (ICodeGenerated)Activator.CreateInstance(actionType)!;
            return (instance, null);
        }

        return (null, Errors.ActionError.NotFound($"Action '{module}.{actionName}'", context));
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

    public void Clear()
    {
        _handlers.Clear();
        _actionTypes.Clear();
    }

    /// <summary>
    /// Gets the CLR type for a handler, checking both explicit and type-registered handlers.
    /// </summary>
    public Type? GetActionType(string module, string actionName)
    {
        // Check explicit instances first
        var handler = Get(module, actionName);
        if (handler != null)
            return handler.GetType();

        if (_actionTypes.TryGetValue(module, out var actionTypes) &&
            actionTypes.TryGetValue(actionName, out var type))
            return type;
        return null;
    }

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
}
