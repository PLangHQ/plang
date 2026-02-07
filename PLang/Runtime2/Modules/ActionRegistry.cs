using System.Collections.Concurrent;
using System.Reflection;

namespace PLang.Runtime2.Modules;

public sealed class ActionRegistry
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, IClass>> _handlers = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string ns, string className, IClass handler)
    {
        var classes = _handlers.GetOrAdd(ns, _ => new ConcurrentDictionary<string, IClass>(StringComparer.OrdinalIgnoreCase));
        classes[className] = handler;
    }

    public void DiscoverAndRegister(Assembly assembly)
    {
        const string baseNs = "PLang.Runtime2.Modules";
        var types = assembly.GetTypes()
            .Where(t => typeof(IClass).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

        foreach (var type in types)
        {
            if (type.Namespace == null || !type.Namespace.StartsWith(baseNs + "."))
                continue;

            var ns = type.Namespace[(baseNs.Length + 1)..];
            var instance = (IClass)Activator.CreateInstance(type)!;

            // Use ParameterType.Name as action name (e.g., "save" from typeof(save).Name)
            // For no-params handlers, strip "Handler" suffix
            var cls = instance.ParameterType?.Name
                ?? (type.Name.EndsWith("Handler")
                    ? type.Name[..^"Handler".Length].ToLowerInvariant()
                    : type.Name);

            Register(ns, cls, instance);
        }
    }

    public IClass? Get(string ns, string className)
    {
        if (string.IsNullOrEmpty(ns) || string.IsNullOrEmpty(className))
            return null;

        if (_handlers.TryGetValue(ns, out var classes) &&
            classes.TryGetValue(className, out var handler))
            return handler;

        return null;
    }

    public bool Contains(string ns, string className)
    {
        return Get(ns, className) != null;
    }

    public bool Contains(string ns)
    {
        return _handlers.ContainsKey(ns);
    }

    public IEnumerable<string> GetClasses(string ns)
    {
        if (_handlers.TryGetValue(ns, out var classes))
            return classes.Keys;
        return Enumerable.Empty<string>();
    }

    public IEnumerable<string> Namespaces => _handlers.Keys;

    public int Count => _handlers.Values.Sum(c => c.Count);

    public void Clear()
    {
        _handlers.Clear();
    }

    public IEnumerable<IClass> All
    {
        get
        {
            foreach (var classes in _handlers.Values)
                foreach (var handler in classes.Values)
                    yield return handler;
        }
    }
}
