using System.Collections.Concurrent;
using PLang.Runtime2.Errors;

namespace PLang.Runtime2.Modules;

/// <summary>
/// Registry for Runtime2 modules.
/// Provides module lookup by name or alias.
/// </summary>
public sealed class ModuleRegistry
{
    private readonly ConcurrentDictionary<string, IModule> _modules = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _aliases = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a module.
    /// </summary>
    public void Register(IModule module)
    {
        _modules[module.Name] = module;

        // Register aliases
        foreach (var alias in module.Aliases)
        {
            _aliases[alias] = module.Name;
        }
    }

    /// <summary>
    /// Registers a module instance by type.
    /// </summary>
    public void Register<T>() where T : IModule, new()
    {
        Register(new T());
    }

    /// <summary>
    /// Gets a module by name or alias.
    /// </summary>
    public IModule? Get(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        // Try direct lookup
        if (_modules.TryGetValue(name, out var module))
            return module;

        // Try alias lookup
        if (_aliases.TryGetValue(name, out var actualName))
        {
            return _modules.TryGetValue(actualName, out module) ? module : null;
        }

        return null;
    }

    /// <summary>
    /// Gets a module by type.
    /// </summary>
    public T? Get<T>() where T : class, IModule
    {
        return _modules.Values.OfType<T>().FirstOrDefault();
    }

    /// <summary>
    /// Gets a module or throws if not found.
    /// </summary>
    public IModule GetRequired(string name)
    {
        var module = Get(name);
        if (module == null)
            throw new ModuleNotFoundException(name);
        return module;
    }

    /// <summary>
    /// Checks if a module exists.
    /// </summary>
    public bool Contains(string name)
    {
        return _modules.ContainsKey(name) || _aliases.ContainsKey(name);
    }

    /// <summary>
    /// Removes a module.
    /// </summary>
    public bool Remove(string name)
    {
        if (_modules.TryRemove(name, out var module))
        {
            // Remove aliases
            foreach (var alias in module.Aliases)
            {
                _aliases.TryRemove(alias, out _);
            }
            return true;
        }
        return false;
    }

    /// <summary>
    /// Clears all modules.
    /// </summary>
    public void Clear()
    {
        _modules.Clear();
        _aliases.Clear();
    }

    /// <summary>
    /// Gets all registered module names.
    /// </summary>
    public IEnumerable<string> Names => _modules.Keys;

    /// <summary>
    /// Gets all registered modules.
    /// </summary>
    public IEnumerable<IModule> All => _modules.Values;

    /// <summary>
    /// Gets the count of registered modules.
    /// </summary>
    public int Count => _modules.Count;

    /// <summary>
    /// Gets all aliases.
    /// </summary>
    public IReadOnlyDictionary<string, string> Aliases => _aliases;

    /// <summary>
    /// Finds modules that can handle a given method.
    /// </summary>
    public IEnumerable<IModule> FindByMethod(string method)
    {
        return _modules.Values.Where(m => m.CanHandle(method));
    }
}
