using System.Collections.Concurrent;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.crypto.providers;

namespace PLang.Runtime2.Engine.Providers;

/// <summary>
/// Named provider registry. Each provider interface type can have multiple named implementations.
/// First registered becomes default. Thread-safe via ConcurrentDictionary.
/// Generic methods delegate to non-generic — single source of truth for all logic.
/// </summary>
public sealed class @this
{
    private readonly ConcurrentDictionary<System.Type, ConcurrentDictionary<string, IProvider>> _providers = new();

    // --- Generic convenience methods (delegate to non-generic) ---

    /// <summary>
    /// Registers a named provider. First registered for a type becomes default.
    /// </summary>
    public Data Register<T>(T provider) where T : class, IProvider
        => Register(typeof(T), provider);

    /// <summary>
    /// Gets a provider by name, or the default if name is null/empty.
    /// Returns typed Data&lt;T&gt; with error if not found.
    /// </summary>
    public Data<T> Get<T>(string? name = null) where T : class, IProvider
    {
        if (!_providers.TryGetValue(typeof(T), out var typeDict))
            return Data<T>.FromError(new ActionError($"No {typeof(T).Name} provider registered", "ProviderNotFound", 404));

        if (!string.IsNullOrEmpty(name))
        {
            if (!typeDict.TryGetValue(name, out var provider))
                return Data<T>.FromError(new ActionError($"Provider '{name}' not found for {typeof(T).Name}", "ProviderNotFound", 404));
            return Data<T>.Ok((T)provider);
        }

        foreach (var kvp in typeDict)
        {
            if (kvp.Value.IsDefault)
                return Data<T>.Ok((T)kvp.Value);
        }

        return Data<T>.FromError(new ActionError($"No default {typeof(T).Name} provider registered", "ProviderNotFound", 404));
    }

    /// <summary>
    /// Gets the default, or the provided fallback if none registered.
    /// </summary>
    public T GetOrDefault<T>(T defaultProvider) where T : class, IProvider
    {
        var result = Get<T>();
        return result.Success ? result.Value! : defaultProvider;
    }

    /// <summary>
    /// Removes a provider by name. Cannot remove the default.
    /// </summary>
    public Data Remove<T>(string name) where T : class, IProvider
        => Remove(typeof(T), name);

    /// <summary>
    /// Sets a named provider as the default for its type.
    /// </summary>
    public Data SetDefault<T>(string name) where T : class, IProvider
        => SetDefault(typeof(T), name);

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

    // --- Non-generic methods (single source of truth) ---

    /// <summary>
    /// Registers a provider by runtime-resolved type. First registered for a type becomes default.
    /// </summary>
    public Data Register(System.Type providerType, IProvider provider)
    {
        var typeDict = _providers.GetOrAdd(providerType, _ => new ConcurrentDictionary<string, IProvider>(StringComparer.OrdinalIgnoreCase));

        if (!typeDict.TryAdd(provider.Name, provider))
            return Data.FromError(new ActionError($"Provider '{provider.Name}' already registered for {providerType.Name}", "ProviderExists", 409));

        if (typeDict.Count == 1)
            provider.IsDefault = true;

        return Data.Ok(provider);
    }

    /// <summary>
    /// Lists all providers for a runtime-resolved type.
    /// </summary>
    public Data List(System.Type providerType)
    {
        if (!_providers.TryGetValue(providerType, out var typeDict))
            return Data.Ok(Array.Empty<IProvider>());

        return Data.Ok(typeDict.Values.ToList());
    }

    /// <summary>
    /// Removes a named provider by runtime-resolved type. Cannot remove the default.
    /// </summary>
    public Data Remove(System.Type providerType, string name)
    {
        if (string.IsNullOrEmpty(name))
            return Data.FromError(new ActionError("Provider name is required", "ValidationError", 400));

        if (!_providers.TryGetValue(providerType, out var typeDict))
            return Data.FromError(new ActionError($"Provider '{name}' not found", "ProviderNotFound", 404));

        if (!typeDict.TryGetValue(name, out var provider))
            return Data.FromError(new ActionError($"Provider '{name}' not found", "ProviderNotFound", 404));

        if (provider.IsDefault)
            return Data.FromError(new ActionError($"Cannot remove default provider '{name}'. Set another as default first.", "CannotRemoveDefault", 400));

        typeDict.TryRemove(name, out _);
        return Data.Ok();
    }

    /// <summary>
    /// Sets a named provider as default by runtime-resolved type.
    /// </summary>
    public Data SetDefault(System.Type providerType, string name)
    {
        if (string.IsNullOrEmpty(name))
            return Data.FromError(new ActionError("Provider name is required", "ValidationError", 400));

        if (!_providers.TryGetValue(providerType, out var typeDict))
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
    /// Resolves a PLang provider type name to its CLR type.
    /// Owns the mapping — callers don't need to know interface types.
    /// </summary>
    public System.Type? ResolveType(string? typeName)
    {
        return typeName?.ToLowerInvariant() switch
        {
            "signing" or "isigningprovider" => typeof(ISigningProvider),
            "key" or "ikeyprovider" => typeof(IKeyProvider),
            "identity" or "iidentityprovider" => typeof(IIdentityProvider),
            "crypto" or "icryptoprovider" => typeof(ICryptoProvider),
            "http" or "ihttpprovider" => typeof(modules.http.providers.IHttpProvider),
            null or "" => typeof(ISigningProvider),
            _ => null
        };
    }
}
