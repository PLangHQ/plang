using System.Collections.Concurrent;

namespace PLang.Runtime2.Engine.Providers;

/// <summary>
/// Type-keyed service registry for pluggable module implementations.
/// Each module defines a provider interface (e.g., ICryptoProvider). Modules register
/// their default implementation. PLang developers override by loading a DLL that
/// implements the same interface.
///
/// PLang: "set crypto provider my.dll" → registers ICryptoProvider from my.dll
/// Handler: engine.Providers.Get&lt;ICryptoProvider&gt;() ?? new DefaultProvider()
/// </summary>
public sealed class @this
{
    private readonly ConcurrentDictionary<Type, object> _providers = new();

    /// <summary>
    /// Registers a provider. Replaces any existing registration for the same type.
    /// </summary>
    public void Register<T>(T provider) where T : class
    {
        _providers[typeof(T)] = provider;
    }

    /// <summary>
    /// Gets a registered provider, or null if none registered.
    /// </summary>
    public T? Get<T>() where T : class
    {
        return _providers.TryGetValue(typeof(T), out var p) ? (T)p : null;
    }

    /// <summary>
    /// Gets a registered provider, or the default if none registered.
    /// </summary>
    public T GetOrDefault<T>(T defaultProvider) where T : class
    {
        return Get<T>() ?? defaultProvider;
    }

    /// <summary>
    /// Checks if a provider is registered for the given type.
    /// </summary>
    public bool Has<T>() where T : class
    {
        return _providers.ContainsKey(typeof(T));
    }

    /// <summary>
    /// Removes a provider registration.
    /// </summary>
    public bool Remove<T>() where T : class
    {
        return _providers.TryRemove(typeof(T), out _);
    }
}
