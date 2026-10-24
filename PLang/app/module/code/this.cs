using System.Collections.Concurrent;
using app.error;
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
    private bool _disposed;

    /// <summary>
    /// The context this registry births its result Data from. The registry is a
    /// system-owned collection, born with the App's system context.
    /// </summary>
    private readonly actor.context.@this Context;

    // whether the registry holds the built-in providers: an app's does, a bare registry holds none
    private readonly bool _builtIn;

    public @this(actor.context.@this context) : this(context, builtIn: false) { }

    /// <summary>The registry of <paramref name="context"/>'s app, holding the built-in providers when
    /// <paramref name="builtIn"/> — registered at its first use, so an app that asks for none makes none.</summary>
    internal @this(actor.context.@this context, bool builtIn)
    {
        Context = context;
        _builtIn = builtIn;
    }

    // The registry's tables, made at its first use — with the built-in providers when it holds them.
    private sealed class tables
    {
        internal readonly ConcurrentDictionary<System.Type, ConcurrentDictionary<string, ICode>> Providers = new();
        // The default's name per interface. A default is selection, and selection is the registry's: one
        // provider can serve several interfaces (Ed25519 is ISigning and IKey), each with its own default.
        internal readonly ConcurrentDictionary<System.Type, string> Defaults = new();
        // The default each interface was born with. Snapshot capture compares the current default against it to
        // tell a user's selection from what a fresh App sets itself.
        internal readonly ConcurrentDictionary<System.Type, string> BuiltIn = new();
    }

    private tables? _tables;
    private readonly object _gate = new();

    // The tables as they are now: made, with the built-ins registered, on the first use — the one door every other
    // reads through.
    private tables Tables
    {
        get
        {
            if (System.Threading.Volatile.Read(ref _tables) is { } made) return made;
            lock (_gate)
            {
                if (_tables is { } held) return held;
                var tables = new tables();
                if (_builtIn) BuiltIns(tables);
                System.Threading.Volatile.Write(ref _tables, tables);
                return tables;
            }
        }
    }

    private ConcurrentDictionary<System.Type, ConcurrentDictionary<string, ICode>> _providers => Tables.Providers;
    private ConcurrentDictionary<System.Type, string> _defaults => Tables.Defaults;
    private ConcurrentDictionary<System.Type, string> _builtInDefaults => Tables.BuiltIn;

    /// <summary>
    /// Disposes every registered provider instance (IAsyncDisposable preferred,
    /// IDisposable fallback). Same projection as <see cref="All"/>.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        // a registry never used made no provider, so it has none to dispose
        if (System.Threading.Volatile.Read(ref _tables) is not { } tables) return;
        foreach (var provider in tables.Providers.Values.SelectMany(p => p.Values))
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
    public (T? Provider, global::app.error.@this? Error) Get<T>(string? name = null) where T : class, ICode
    {
        if (!_providers.TryGetValue(typeof(T), out var typeDict))
            return (null, new ActionError($"No {typeof(T).Name} provider registered", "ProviderNotFound", 404));

        if (!string.IsNullOrEmpty(name))
        {
            if (!typeDict.TryGetValue(name, out var provider))
                return (null, new ActionError($"Provider '{name}' not found for {typeof(T).Name}", "ProviderNotFound", 404));
            return ((T)provider, null);
        }

        if (_defaults.TryGetValue(typeof(T), out var defaultName) && typeDict.TryGetValue(defaultName, out var chosen))
            return ((T)chosen, null);

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
        // A provider is engine plumbing, never a plang value — the registration
        // just succeeds. Returning the provider as the value would wrap a CLR
        // service in a Data (a throwaway clr); the value is never read.
        => Put(Tables, providerType, provider) ? Context.Ok()
            : Context.Error(new ActionError($"Provider '{provider.Name}' already registered for {providerType.Name}", "ProviderExists", 409));

    // Registers provider for providerType in tables; the first for a type becomes its default. False when a provider
    // of its name is already there.
    private bool Put(tables tables, System.Type providerType, ICode provider)
    {
        var typeDict = tables.Providers.GetOrAdd(providerType, _ => new ConcurrentDictionary<string, ICode>(StringComparer.OrdinalIgnoreCase));
        if (!typeDict.TryAdd(provider.Name, provider)) return false;
        tables.Defaults.TryAdd(providerType, provider.Name);
        return true;
    }

    /// <summary>
    /// Lists all providers registered for a runtime-resolved type (typed C# access).
    /// </summary>
    public IReadOnlyList<ICode> List(System.Type providerType)
        => _providers.TryGetValue(providerType, out var typeDict)
            ? typeDict.Values.ToList()
            : Array.Empty<ICode>();

    /// <summary>
    /// Removes a named provider by runtime-resolved type. Cannot remove the default.
    /// </summary>
    public data.@this Remove(System.Type providerType, string name)
    {
        if (string.IsNullOrEmpty(name))
            return Context.Error(new ActionError("Provider name is required", "ValidationError", 400));

        if (!_providers.TryGetValue(providerType, out var typeDict))
            return Context.Error(new ActionError($"Provider '{name}' not found", "ProviderNotFound", 404));

        if (!typeDict.TryGetValue(name, out var provider))
            return Context.Error(new ActionError($"Provider '{name}' not found", "ProviderNotFound", 404));

        if (_defaults.TryGetValue(providerType, out var defaultName)
            && string.Equals(defaultName, provider.Name, StringComparison.OrdinalIgnoreCase))
            return Context.Error(new ActionError($"Cannot remove default provider '{name}'. Set another as default first.", "CannotRemoveDefault", 400));

        typeDict.TryRemove(name, out _);
        return Context.Ok();
    }

    /// <summary>
    /// Sets a named provider as default by runtime-resolved type.
    /// </summary>
    public data.@this SetDefault(System.Type providerType, string name)
    {
        if (string.IsNullOrEmpty(name))
            return Context.Error(new ActionError("Provider name is required", "ValidationError", 400));

        if (!_providers.TryGetValue(providerType, out var typeDict))
            return Context.Error(new ActionError($"Provider '{name}' not found", "ProviderNotFound", 404));

        if (!typeDict.TryGetValue(name, out var newDefault))
            return Context.Error(new ActionError($"Provider '{name}' not found", "ProviderNotFound", 404));

        // One slot per interface — the provider's other interfaces keep their own default.
        _defaults[providerType] = newDefault.Name;
        return Context.Ok();
    }

    // The built-in providers, into tables as they are made. Each module owns its default provider — this is the single
    // registration point. Every built-in is stamped so the snapshot capture skips them: a fresh App's registry holds
    // the same set, so they don't ride along in the payload.
    private void BuiltIns(tables tables)
    {
        var ed25519 = new Ed25519();
        BuiltIn<ISigning>(tables, ed25519);
        BuiltIn<IKey>(tables, ed25519);
        BuiltIn<IIdentity>(tables, new global::app.module.identity.code.Default());
        BuiltIn<ICrypto>(tables, new global::app.module.crypto.code.Default());
        BuiltIn<global::app.module.math.code.IMath>(tables, new global::app.module.math.code.Default());
        BuiltIn<global::app.module.http.code.IHttp>(tables, new global::app.module.http.code.Default());
        BuiltIn<global::app.module.archive.code.IArchive>(tables, new global::app.module.archive.code.Default());
        BuiltIn<global::app.module.terminal.code.ITerminal>(tables, new global::app.module.terminal.code.Default());
        BuiltIn<global::app.module.browser.code.IBrowser>(tables, new global::app.module.browser.code.Chromium());
        // the screen: on Windows a window that shows frames; on Linux (PlangOS) the display that makes them
        BuiltIn<global::app.module.screen.code.IScreen>(tables, OperatingSystem.IsWindows()
            ? new global::app.module.screen.code.Default()
            : new global::app.module.screen.code.Wayland());
        BuiltIn<global::app.module.condition.code.IEvaluator>(tables, new global::app.module.condition.code.Default());
        BuiltIn<global::app.module.assert.code.IAssert>(tables, new global::app.module.assert.code.Default());
        BuiltIn<ITemplate>(tables, new global::app.module.ui.code.Fluid());
        BuiltIn<global::app.module.llm.code.ILlm>(tables, new global::app.module.llm.code.OpenAi());
        BuiltIn<global::app.module.llm.code.IDecider>(tables, new global::app.module.llm.code.TypeSafe());
        BuiltIn<global::app.module.build.code.IBuilder>(tables, new global::app.module.build.code.Default());
        BuiltIn<global::app.data.code.IGrep>(tables, new global::app.data.code.Default());
        BuiltIn<global::app.store.code.IStore>(tables, new global::app.store.code.Sqlite());
    }

    private void BuiltIn<T>(tables tables, T provider) where T : class, ICode
    {
        provider.IsBuiltIn = true;
        Put(tables, typeof(T), provider);
        // the type's "natural" default name, once — the first built-in for a type becomes its default by being first
        tables.BuiltIn.TryAdd(typeof(T), provider.Name);
    }
}
