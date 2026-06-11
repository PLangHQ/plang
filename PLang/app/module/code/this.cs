using System.Collections.Concurrent;
using app.error;
using app.variable;
using app.module.crypto.code;
using app.module.identity.code;
using app.module.signing.code;
using app.module.ui.code;

namespace app.module.code;

/// <summary>
/// Named provider registry. Each provider interface type can have multiple named implementations.
/// First registered becomes default. Thread-safe via ConcurrentDictionary.
/// Generic methods delegate to non-generic — single source of truth for all logic.
/// </summary>
public sealed partial class @this : IAsyncDisposable
{
    private readonly ConcurrentDictionary<System.Type, ConcurrentDictionary<string, ICode>> _providers = new();
    private bool _disposed;

    // Remembers which provider RegisterDefaults marked as the type's default. Used by
    // Snapshot capture to decide whether the *current* default differs from what a
    // freshly-booted App would set. Without this, SetDefault() clearing the built-in's
    // IsDefault flag would erase the evidence needed to detect the override.
    private readonly ConcurrentDictionary<System.Type, string> _builtInDefaults = new();

    /// <summary>
    /// Disposes every registered provider instance (IAsyncDisposable preferred,
    /// IDisposable fallback). Same projection as <see cref="All"/>.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var provider in _providers.Values.SelectMany(p => p.Values))
        {
            if (provider is IAsyncDisposable asyncProv)
                await asyncProv.DisposeAsync();
            else if (provider is IDisposable disposableProv)
                disposableProv.Dispose();
        }
    }

    // --- Generic convenience methods (delegate to non-generic) ---

    /// <summary>
    /// Registers a named provider. First registered for a type becomes default.
    /// </summary>
    public data.@this Register<T>(T provider) where T : class, ICode
        => Register(typeof(T), provider);

    /// <summary>
    /// Gets a provider by name, or the default if name is null/empty.
    /// Returns typed Data&lt;T&gt; with error if not found.
    /// </summary>
    // A code provider is a CLR capability, never a PLang value — it rides in a BARE Data
    // (no generic T → satisfies where T:item) with the provider in .Value. Callers read
    // .Success/.Value (cast to the provider type).
    // A provider is engine plumbing — rung-3, NEVER a plang value, so it never
    // rides a Data (no clr-carrier round-trip, no Peek to unwrap). The typed
    // provider comes back directly beside a typed error.
    public (T? Provider, global::app.error.IError? Error) Get<T>(string? name = null) where T : class, ICode
    {
        if (!_providers.TryGetValue(typeof(T), out var typeDict))
            return (null, new ActionError($"No {typeof(T).Name} provider registered", "ProviderNotFound", 404));

        if (!string.IsNullOrEmpty(name))
        {
            if (!typeDict.TryGetValue(name, out var provider))
                return (null, new ActionError($"Provider '{name}' not found for {typeof(T).Name}", "ProviderNotFound", 404));
            return ((T)provider, null);
        }

        foreach (var kvp in typeDict)
            if (kvp.Value.IsDefault)
                return ((T)kvp.Value, null);

        return (null, new ActionError($"No default {typeof(T).Name} provider registered", "ProviderNotFound", 404));
    }

    /// <summary>
    /// Gets the default, or the provided fallback if none registered.
    /// </summary>
    public T GetOrDefault<T>(T defaultProvider) where T : class, ICode
        => Get<T>().Provider ?? defaultProvider;

    /// <summary>
    /// Removes a provider by name. Cannot remove the default.
    /// </summary>
    public data.@this Remove<T>(string name) where T : class, ICode
        => Remove(typeof(T), name);

    /// <summary>
    /// Sets a named provider as the default for its type.
    /// </summary>
    public data.@this SetDefault<T>(string name) where T : class, ICode
        => SetDefault(typeof(T), name);

    /// <summary>
    /// Lists all providers for a specific type.
    /// </summary>
    public IReadOnlyList<T> List<T>() where T : class, ICode
    {
        if (!_providers.TryGetValue(typeof(T), out var typeDict))
            return Array.Empty<T>();

        return typeDict.Values.Cast<T>().ToList();
    }

    /// <summary>
    /// Lists all providers across all types.
    /// </summary>
    public IReadOnlyList<ICode> List()
    {
        var result = new List<ICode>();
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
    public IEnumerable<ICode> All() => _providers.Values
        .SelectMany(dict => dict.Values);

    public bool Has<T>() where T : class, ICode
    {
        return _providers.TryGetValue(typeof(T), out var typeDict) && !typeDict.IsEmpty;
    }

    // --- Non-generic methods (single source of truth) ---

    /// <summary>
    /// Registers a provider by runtime-resolved type. First registered for a type becomes default.
    /// </summary>
    public data.@this Register(System.Type providerType, ICode provider)
    {
        var typeDict = _providers.GetOrAdd(providerType, _ => new ConcurrentDictionary<string, ICode>(StringComparer.OrdinalIgnoreCase));

        if (!typeDict.TryAdd(provider.Name, provider))
            return app.data.@this.FromError(new ActionError($"Provider '{provider.Name}' already registered for {providerType.Name}", "ProviderExists", 409));

        if (typeDict.Count == 1)
            provider.IsDefault = true;

        return app.data.@this.Ok(provider);
    }

    /// <summary>
    /// Lists all providers for a runtime-resolved type.
    /// </summary>
    public data.@this List(System.Type providerType)
    {
        if (!_providers.TryGetValue(providerType, out var typeDict))
            return app.data.@this.Ok(Array.Empty<ICode>());

        return app.data.@this.Ok(typeDict.Values.ToList());
    }

    /// <summary>
    /// Removes a named provider by runtime-resolved type. Cannot remove the default.
    /// </summary>
    public data.@this Remove(System.Type providerType, string name)
    {
        if (string.IsNullOrEmpty(name))
            return app.data.@this.FromError(new ActionError("Provider name is required", "ValidationError", 400));

        if (!_providers.TryGetValue(providerType, out var typeDict))
            return app.data.@this.FromError(new ActionError($"Provider '{name}' not found", "ProviderNotFound", 404));

        if (!typeDict.TryGetValue(name, out var provider))
            return app.data.@this.FromError(new ActionError($"Provider '{name}' not found", "ProviderNotFound", 404));

        if (provider.IsDefault)
            return app.data.@this.FromError(new ActionError($"Cannot remove default provider '{name}'. Set another as default first.", "CannotRemoveDefault", 400));

        typeDict.TryRemove(name, out _);
        return app.data.@this.Ok();
    }

    /// <summary>
    /// Sets a named provider as default by runtime-resolved type.
    /// </summary>
    public data.@this SetDefault(System.Type providerType, string name)
    {
        if (string.IsNullOrEmpty(name))
            return app.data.@this.FromError(new ActionError("Provider name is required", "ValidationError", 400));

        if (!_providers.TryGetValue(providerType, out var typeDict))
            return app.data.@this.FromError(new ActionError($"Provider '{name}' not found", "ProviderNotFound", 404));

        if (!typeDict.TryGetValue(name, out var newDefault))
            return app.data.@this.FromError(new ActionError($"Provider '{name}' not found", "ProviderNotFound", 404));

        // Set new default first, then clear old — avoids window where Get<T>() returns null
        newDefault.IsDefault = true;
        foreach (var kvp in typeDict)
        {
            if (kvp.Value != newDefault)
                kvp.Value.IsDefault = false;
        }
        return app.data.@this.Ok();
    }

    /// <summary>
    /// Resolves a PLang provider type name to its CLR type.
    /// Owns the mapping — callers don't need to know interface types.
    /// </summary>
    public System.Type? ResolveType(string? typeName)
    {
        return typeName?.ToLowerInvariant() switch
        {
            "signing" or "isigningprovider" => typeof(ISigning),
            "key" or "ikeyprovider" => typeof(IKey),
            "identity" or "iidentityprovider" => typeof(IIdentity),
            "crypto" or "icryptoprovider" => typeof(ICrypto),
            "http" or "ihttpprovider" => typeof(global::app.module.http.code.IHttp),
            "evaluator" or "ievaluator" => typeof(global::app.module.condition.code.IEvaluator),
            "assert" or "iassertprovider" => typeof(global::app.module.assert.code.IAssert),
            // "file" / "ifileprovider" removed — file actions no longer route through a
            // [Code]-partial provider; FilePath holds the verb impls directly.
            "template" or "itemplateprovider" => typeof(ITemplate),
            "llm" or "illmprovider" => typeof(global::app.module.llm.code.ILlm),
            "builder" or "ibuilderprovider" => typeof(global::app.module.builder.code.IBuilder),
            null or "" => typeof(ISigning),
            _ => null
        };
    }

    /// <summary>
    /// Registers built-in default providers. Called by App constructor.
    /// Each module owns its default provider — this method is the single registration point.
    /// </summary>
    public void RegisterDefaults()
    {
        // Stamp every built-in registration so the snapshot capture skips them —
        // the fresh App's RegisterDefaults reproduces the same set on Restore, so
        // they don't need to ride along in the payload.
        var ed25519 = new Ed25519();
        RegisterBuiltIn<ISigning>(ed25519);
        RegisterBuiltIn<IKey>(ed25519);
        RegisterBuiltIn<IIdentity>(new global::app.module.identity.code.Default());
        RegisterBuiltIn<ICrypto>(new global::app.module.crypto.code.Default());
        RegisterBuiltIn<global::app.module.http.code.IHttp>(new global::app.module.http.code.Default());
        RegisterBuiltIn<global::app.module.condition.code.IEvaluator>(new global::app.module.condition.code.Default());
        RegisterBuiltIn<global::app.module.assert.code.IAssert>(new global::app.module.assert.code.Default());
        // global::app.module.file.code.IFile registration removed in Stage 3.
        RegisterBuiltIn<ITemplate>(new global::app.module.ui.code.Fluid());
        RegisterBuiltIn<global::app.module.llm.code.ILlm>(new global::app.module.llm.code.OpenAi());
        RegisterBuiltIn<global::app.module.builder.code.IBuilder>(new global::app.module.builder.code.Default());
        RegisterBuiltIn<global::app.data.code.IGrep>(new global::app.data.code.Default());
    }

    private void RegisterBuiltIn<T>(T provider) where T : class, ICode
    {
        provider.IsBuiltIn = true;
        Register(typeof(T), provider);
        // Stamp the type's "natural" default name *once* — the first built-in for a type
        // becomes default by virtue of being first-registered, mirroring Register's contract.
        _builtInDefaults.TryAdd(typeof(T), provider.Name);
    }
}
