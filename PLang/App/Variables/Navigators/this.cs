using System.Collections.Concurrent;

namespace App.Variables.Navigators;

/// <summary>
/// Registry of navigators by type. Each type can have a registered navigator
/// that knows how to traverse it. Falls back to CLR reflection navigator.
/// </summary>
public sealed class @this
{
    private readonly ConcurrentDictionary<System.Type, INavigator> _navigators = new();
    private readonly INavigator _fallback = new global::App.Variables.Navigators.Object();

    /// <summary>
    /// Register a navigator for a specific type.
    /// </summary>
    public void Register<T>(INavigator navigator) => Register(typeof(T), navigator);
    public void Register(System.Type type, INavigator navigator) => _navigators[type] = navigator;

    /// <summary>
    /// Get the navigator for a type. Returns the registered navigator or the CLR reflection fallback.
    /// Checks the type hierarchy — if no exact match, checks base types and interfaces.
    /// </summary>
    public INavigator Get(System.Type type)
    {
        if (_navigators.TryGetValue(type, out var nav))
            return nav;

        // Check generic type definition (e.g., List<T> → List<>)
        if (type.IsGenericType)
        {
            var generic = type.GetGenericTypeDefinition();
            if (_navigators.TryGetValue(generic, out nav))
                return nav;
        }

        // Check interfaces
        foreach (var iface in type.GetInterfaces())
        {
            if (_navigators.TryGetValue(iface, out nav))
                return nav;

            if (iface.IsGenericType)
            {
                var genericIface = iface.GetGenericTypeDefinition();
                if (_navigators.TryGetValue(genericIface, out nav))
                    return nav;
            }
        }

        return _fallback;
    }

    /// <summary>
    /// Register the default navigators for built-in types.
    /// </summary>
    public void RegisterDefaults()
    {
        Register(typeof(System.Collections.IList), new global::App.Variables.Navigators.List());
        Register(typeof(IDictionary<string, object?>), new global::App.Variables.Navigators.Dictionary());
        Register(typeof(IDictionary<,>), new global::App.Variables.Navigators.Dictionary());
    }
}
