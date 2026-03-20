using System.Collections.Concurrent;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.Engine.Providers;

/// <summary>
/// Named provider registry. Each provider interface type can have multiple named implementations.
/// First registered becomes default. Thread-safe via ConcurrentDictionary.
/// </summary>
public sealed class @this
{
    private readonly ConcurrentDictionary<System.Type, ConcurrentDictionary<string, IProvider>> _providers = new();

    /// <summary>
    /// Registers a named provider. First registered for a type becomes default.
    /// Returns error if name already exists for the same type.
    /// </summary>
    public Data Register<T>(T provider) where T : class, IProvider
    {
        var typeDict = _providers.GetOrAdd(typeof(T), _ => new ConcurrentDictionary<string, IProvider>(StringComparer.OrdinalIgnoreCase));

        if (!typeDict.TryAdd(provider.Name, provider))
            return Data.FromError(new ActionError($"Provider '{provider.Name}' already registered for {typeof(T).Name}", "ProviderExists", 409));

        // First registered = auto-default
        if (typeDict.Count == 1)
            provider.IsDefault = true;

        return Data.Ok(provider);
    }

    /// <summary>
    /// Gets the default provider for a type, or null if none registered.
    /// </summary>
    public T? Get<T>() where T : class, IProvider
    {
        if (!_providers.TryGetValue(typeof(T), out var typeDict))
            return null;

        foreach (var kvp in typeDict)
        {
            if (kvp.Value.IsDefault)
                return (T)kvp.Value;
        }

        return null;
    }

    /// <summary>
    /// Gets a provider by name, or null if not found.
    /// </summary>
    public T? Get<T>(string name) where T : class, IProvider
    {
        if (!_providers.TryGetValue(typeof(T), out var typeDict))
            return null;

        return typeDict.TryGetValue(name, out var provider) ? (T)provider : null;
    }

    /// <summary>
    /// Gets the default, or the provided fallback if none registered.
    /// </summary>
    public T GetOrDefault<T>(T defaultProvider) where T : class, IProvider
    {
        return Get<T>() ?? defaultProvider;
    }

    /// <summary>
    /// Removes a provider by name. Cannot remove the default.
    /// </summary>
    public Data Remove<T>(string name) where T : class, IProvider
    {
        if (!_providers.TryGetValue(typeof(T), out var typeDict))
            return Data.FromError(new ActionError($"Provider '{name}' not found", "ProviderNotFound", 404));

        if (!typeDict.TryGetValue(name, out var provider))
            return Data.FromError(new ActionError($"Provider '{name}' not found", "ProviderNotFound", 404));

        if (provider.IsDefault)
            return Data.FromError(new ActionError($"Cannot remove default provider '{name}'. Set another as default first.", "CannotRemoveDefault", 400));

        typeDict.TryRemove(name, out _);
        return Data.Ok();
    }

    /// <summary>
    /// Sets a named provider as the default for its type.
    /// </summary>
    public Data SetDefault<T>(string name) where T : class, IProvider
    {
        if (!_providers.TryGetValue(typeof(T), out var typeDict))
            return Data.FromError(new ActionError($"Provider '{name}' not found", "ProviderNotFound", 404));

        if (!typeDict.TryGetValue(name, out var newDefault))
            return Data.FromError(new ActionError($"Provider '{name}' not found", "ProviderNotFound", 404));

        // Set new default first, then clear old — avoids window where Get<T>() returns null
        newDefault.IsDefault = true;
        foreach (var kvp in typeDict)
        {
            if (kvp.Value != newDefault)
                kvp.Value.IsDefault = false;
        }
        return Data.Ok();
    }

    /// <summary>
    /// Lists all providers for a specific type.
    /// </summary>
    public IReadOnlyList<T> List<T>() where T : class, IProvider
    {
        if (!_providers.TryGetValue(typeof(T), out var typeDict))
            return Array.Empty<T>();

        return typeDict.Values.Cast<T>().ToList();
    }

    /// <summary>
    /// Lists all providers across all types.
    /// </summary>
    public IReadOnlyList<IProvider> List()
    {
        var result = new List<IProvider>();
        foreach (var typeDict in _providers.Values)
            result.AddRange(typeDict.Values);
        return result;
    }

    /// <summary>
    /// Checks if any provider is registered for the given type.
    /// </summary>
    public bool Has<T>() where T : class, IProvider
    {
        return _providers.TryGetValue(typeof(T), out var typeDict) && !typeDict.IsEmpty;
    }
}
