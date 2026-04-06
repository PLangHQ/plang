using System.Collections.Concurrent;
using App.Errors;
using App.Variables;
using App.modules.crypto.providers;
using App.modules.identity.providers;
using App.modules.signing.providers;
using App.modules.ui.providers;

namespace App.Providers;

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
    public Data.@this Register<T>(T provider) where T : class, IProvider
        => Register(typeof(T), provider);

    /// <summary>
    /// Gets a provider by name, or the default if name is null/empty.
    /// Returns typed Data&lt;T&gt; with error if not found.
    /// </summary>
    public Data.@this<T> Get<T>(string? name = null) where T : class, IProvider
    {
        if (!_providers.TryGetValue(typeof(T), out var typeDict))
            return Data.@this<T>.FromError(new ActionError($"No {typeof(T).Name} provider registered", "ProviderNotFound", 404));

        if (!string.IsNullOrEmpty(name))
        {
            if (!typeDict.TryGetValue(name, out var provider))
                return Data.@this<T>.FromError(new ActionError($"Provider '{name}' not found for {typeof(T).Name}", "ProviderNotFound", 404));
            return Data.@this<T>.Ok((T)provider);
        }

        foreach (var kvp in typeDict)
        {
            if (kvp.Value.IsDefault)
                return Data.@this<T>.Ok((T)kvp.Value);
        }

        return Data.@this<T>.FromError(new ActionError($"No default {typeof(T).Name} provider registered", "ProviderNotFound", 404));
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
    public Data.@this Remove<T>(string name) where T : class, IProvider
        => Remove(typeof(T), name);

    /// <summary>
    /// Sets a named provider as the default for its type.
    /// </summary>
    public Data.@this SetDefault<T>(string name) where T : class, IProvider
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
    /// <summary>
    /// Iterates all provider instances across all types.
    /// Used for disposal and inspection.
    /// </summary>
    public IEnumerable<IProvider> All() => _providers.Values
        .SelectMany(dict => dict.Values);

    public bool Has<T>() where T : class, IProvider
    {
        return _providers.TryGetValue(typeof(T), out var typeDict) && !typeDict.IsEmpty;
    }

    // --- Non-generic methods (single source of truth) ---

    /// <summary>
    /// Registers a provider by runtime-resolved type. First registered for a type becomes default.
    /// </summary>
    public Data.@this Register(System.Type providerType, IProvider provider)
    {
        var typeDict = _providers.GetOrAdd(providerType, _ => new ConcurrentDictionary<string, IProvider>(StringComparer.OrdinalIgnoreCase));

        if (!typeDict.TryAdd(provider.Name, provider))
            return App.Data.@this.FromError(new ActionError($"Provider '{provider.Name}' already registered for {providerType.Name}", "ProviderExists", 409));

        if (typeDict.Count == 1)
            provider.IsDefault = true;

        return App.Data.@this.Ok(provider);
    }

    /// <summary>
    /// Lists all providers for a runtime-resolved type.
    /// </summary>
    public Data.@this List(System.Type providerType)
    {
        if (!_providers.TryGetValue(providerType, out var typeDict))
            return App.Data.@this.Ok(Array.Empty<IProvider>());

        return App.Data.@this.Ok(typeDict.Values.ToList());
    }

    /// <summary>
    /// Removes a named provider by runtime-resolved type. Cannot remove the default.
    /// </summary>
    public Data.@this Remove(System.Type providerType, string name)
    {
        if (string.IsNullOrEmpty(name))
            return App.Data.@this.FromError(new ActionError("Provider name is required", "ValidationError", 400));

        if (!_providers.TryGetValue(providerType, out var typeDict))
            return App.Data.@this.FromError(new ActionError($"Provider '{name}' not found", "ProviderNotFound", 404));

        if (!typeDict.TryGetValue(name, out var provider))
            return App.Data.@this.FromError(new ActionError($"Provider '{name}' not found", "ProviderNotFound", 404));

        if (provider.IsDefault)
            return App.Data.@this.FromError(new ActionError($"Cannot remove default provider '{name}'. Set another as default first.", "CannotRemoveDefault", 400));

        typeDict.TryRemove(name, out _);
        return App.Data.@this.Ok();
    }

    /// <summary>
    /// Sets a named provider as default by runtime-resolved type.
    /// </summary>
    public Data.@this SetDefault(System.Type providerType, string name)
    {
        if (string.IsNullOrEmpty(name))
            return App.Data.@this.FromError(new ActionError("Provider name is required", "ValidationError", 400));

        if (!_providers.TryGetValue(providerType, out var typeDict))
            return App.Data.@this.FromError(new ActionError($"Provider '{name}' not found", "ProviderNotFound", 404));

        if (!typeDict.TryGetValue(name, out var newDefault))
            return App.Data.@this.FromError(new ActionError($"Provider '{name}' not found", "ProviderNotFound", 404));

        // Set new default first, then clear old — avoids window where Get<T>() returns null
        newDefault.IsDefault = true;
        foreach (var kvp in typeDict)
        {
            if (kvp.Value != newDefault)
                kvp.Value.IsDefault = false;
        }
        return App.Data.@this.Ok();
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
            "evaluator" or "ievaluator" => typeof(modules.condition.providers.IEvaluator),
            "assert" or "iassertprovider" => typeof(modules.assert.providers.IAssertProvider),
            "file" or "ifileprovider" => typeof(modules.file.providers.IFileProvider),
            "template" or "itemplateprovider" => typeof(ITemplateProvider),
            "llm" or "illmprovider" => typeof(modules.llm.providers.ILlmProvider),
            "builder" or "ibuilderprovider" => typeof(modules.builder.providers.IBuilderProvider),
            null or "" => typeof(ISigningProvider),
            _ => null
        };
    }

    /// <summary>
    /// Registers built-in default providers. Called by App constructor.
    /// Each module owns its default provider — this method is the single registration point.
    /// </summary>
    public void RegisterDefaults()
    {
        var ed25519 = new Ed25519Provider();
        Register<ISigningProvider>(ed25519);
        Register<IKeyProvider>(ed25519);
        Register<IIdentityProvider>(new DefaultIdentityProvider());
        Register<ICryptoProvider>(new DefaultCryptoProvider());
        Register<modules.http.providers.IHttpProvider>(new modules.http.providers.DefaultHttpProvider());
        Register<modules.condition.providers.IEvaluator>(new modules.condition.providers.DefaultEvaluator());
        Register<modules.assert.providers.IAssertProvider>(new modules.assert.providers.DefaultAssertProvider());
        Register<modules.file.providers.IFileProvider>(new modules.file.providers.DefaultFileProvider());
        Register<ITemplateProvider>(new FluidProvider());
        Register<modules.llm.providers.ILlmProvider>(new modules.llm.providers.OpenAiProvider());
        Register<modules.builder.providers.IBuilderProvider>(new modules.builder.providers.DefaultBuilderProvider());
        Register<Data.Providers.IGrepProvider>(new Data.Providers.DefaultGrepProvider());
    }
}
