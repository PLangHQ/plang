namespace PLang.Runtime2.Modules;

/// <summary>
/// Static accessor for the module registry.
/// Provides convenient access to Runtime2 modules.
/// </summary>
public static class Modules
{
    private static ModuleRegistry? _registry;
    private static readonly object _lock = new();

    /// <summary>
    /// Gets the current module registry.
    /// </summary>
    public static ModuleRegistry Registry
    {
        get
        {
            if (_registry == null)
            {
                lock (_lock)
                {
                    _registry ??= CreateDefaultRegistry();
                }
            }
            return _registry;
        }
    }

    /// <summary>
    /// Sets the module registry (for testing or custom configurations).
    /// </summary>
    public static void SetRegistry(ModuleRegistry registry)
    {
        lock (_lock)
        {
            _registry = registry;
        }
    }

    /// <summary>
    /// Gets a module by type.
    /// </summary>
    public static T? Get<T>() where T : class, IModule
    {
        return Registry.Get<T>();
    }

    /// <summary>
    /// Gets a module by name.
    /// </summary>
    public static IModule? Get(string name)
    {
        return Registry.Get(name);
    }

    /// <summary>
    /// Gets a module by name, cast to type.
    /// </summary>
    public static T? Get<T>(string name) where T : class, IModule
    {
        return Registry.Get(name) as T;
    }

    /// <summary>
    /// Registers a module.
    /// </summary>
    public static void Register(IModule module)
    {
        Registry.Register(module);
    }

    /// <summary>
    /// Registers a module by type.
    /// </summary>
    public static void Register<T>() where T : IModule, new()
    {
        Registry.Register<T>();
    }

    /// <summary>
    /// Creates the default registry with built-in modules.
    /// </summary>
    private static ModuleRegistry CreateDefaultRegistry()
    {
        var registry = new ModuleRegistry();

        // Register built-in modules
        registry.Register(new FileModule());
        registry.Register(new OutputModule());

        return registry;
    }

    /// <summary>
    /// Resets the registry (mainly for testing).
    /// </summary>
    public static void Reset()
    {
        lock (_lock)
        {
            _registry = null;
        }
    }
}
