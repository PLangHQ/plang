using System.Collections.Concurrent;
using System.Reflection;

namespace PLang.Runtime2.modules;

public sealed class ActionRegistry
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, IClass>> _handlers = new(StringComparer.OrdinalIgnoreCase);

    // [Action]-attributed types: stored as Type, new instance created per call (thread safety)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Type>> _actionTypes = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string module, string actionName, IClass handler)
    {
        var actions = _handlers.GetOrAdd(module, _ => new ConcurrentDictionary<string, IClass>(StringComparer.OrdinalIgnoreCase));
        actions[actionName] = handler;
    }

    public void RegisterCodeGenerated(string module, string actionName, Type type)
    {
        var actions = _actionTypes.GetOrAdd(module, _ => new ConcurrentDictionary<string, Type>(StringComparer.OrdinalIgnoreCase));
        actions[actionName] = type;
    }

    public void DiscoverAndRegister(Assembly assembly)
    {
        const string baseNs = "PLang.Runtime2.modules";

        // Existing path: IClass-based handlers
        var classTypes = assembly.GetTypes()
            .Where(t => typeof(IClass).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

        foreach (var type in classTypes)
        {
            if (type.Namespace == null || !type.Namespace.StartsWith(baseNs + "."))
                continue;

            var module = type.Namespace[(baseNs.Length + 1)..];
            var instance = (IClass)Activator.CreateInstance(type)!;

            // Use ParameterType.Name as action name (e.g., "save" from typeof(save).Name)
            // For no-params handlers, strip "Handler" suffix
            var actionName = instance.ParameterType?.Name
                ?? (type.Name.EndsWith("Handler")
                    ? type.Name[..^"Handler".Length].ToLowerInvariant()
                    : type.Name);

            Register(module, actionName, instance);
        }

        // New path: [Action] attribute-based classes
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
    /// Gets a handler and validates it implements ICodeGenerated.
    /// Checks both IClass-based handlers and [Action]-based types.
    /// [Action] types create a new instance per call (thread safety).
    /// </summary>
    public (ICodeGenerated? Handler, Errors.IError? Error) GetCodeGenerated(string module, string actionName, Context.PLangContext context)
    {
        // Check IClass-based handlers first (explicit Register() overrides auto-discovered types)
        var handler = Get(module, actionName);
        if (handler != null)
        {
            if (handler is not ICodeGenerated codeGenerated)
                return (null, new Errors.ActionError($"Handler '{module}.{actionName}' does not implement ICodeGenerated", context, "HandlerError", 500) { ActionModule = module, ActionName = actionName });
            return (codeGenerated, null);
        }

        // Fall back to [Action]-based types (factory: new instance per call)
        if (_actionTypes.TryGetValue(module, out var actionTypes) &&
            actionTypes.TryGetValue(actionName, out var actionType))
        {
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
    /// Gets the CLR type for an [Action]-based handler, or null if not found.
    /// </summary>
    public Type? GetActionType(string module, string actionName)
    {
        if (_actionTypes.TryGetValue(module, out var actionTypes) &&
            actionTypes.TryGetValue(actionName, out var type))
            return type;
        return null;
    }

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
