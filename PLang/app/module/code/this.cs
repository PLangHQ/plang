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
    // The registry's tables — the providers per interface, each interface's default, and the default each was born
    // with — made when the registry is first used: an app that asks for no provider holds none.
    private sealed class Tables
    {
        public ConcurrentDictionary<System.Type, ConcurrentDictionary<string, ICode>> Providers { get; } = new();
        // The default's name per interface. A default is selection, and selection is the registry's: one provider can
        // serve several interfaces (Ed25519 is ISigning and IKey), each with its own default.
        public ConcurrentDictionary<System.Type, string> Defaults { get; } = new();
        // The default each interface was born with at RegisterDefaults. Snapshot capture compares the current default
        // against it to tell a user's selection from what a fresh App sets itself.
        public ConcurrentDictionary<System.Type, string> BuiltInDefaults { get; } = new();
    }

    private Tables? _tables;
    private Tables Held => _tables ?? System.Threading.LazyInitializer.EnsureInitialized(ref _tables, () => new Tables());
    private bool _disposed;

    /// <summary>
    /// The context this registry births its result Data from. The registry is a
    /// system-owned collection, born with the App's system context.
    /// </summary>
    private readonly actor.context.@this Context;

    public @this(actor.context.@this context) => Context = context;


    /// <summary>
    /// Disposes every registered provider instance (IAsyncDisposable preferred,
    /// IDisposable fallback). Same projection as <see cref="All"/>.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var provider in _tables?.Providers.Values.SelectMany(p => p.Values) ?? [])
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
        Ready(typeof(T));
        if (!Held.Providers.TryGetValue(typeof(T), out var typeDict))
            return (null, new ActionError($"No {typeof(T).Name} provider registered", "ProviderNotFound", 404));

        if (!string.IsNullOrEmpty(name))
        {
            if (!typeDict.TryGetValue(name, out var provider))
                return (null, new ActionError($"Provider '{name}' not found for {typeof(T).Name}", "ProviderNotFound", 404));
            return ((T)provider, null);
        }

        if (Held.Defaults.TryGetValue(typeof(T), out var defaultName) && typeDict.TryGetValue(defaultName, out var chosen))
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
        Ready(typeof(T));
        if (!Held.Providers.TryGetValue(typeof(T), out var typeDict))
            return Array.Empty<T>();

        return typeDict.Values.Cast<T>().ToList();
    }

    /// <summary>
    /// Lists all providers across all types.
    /// </summary>
    public IReadOnlyList<ICode> List()
    {
        Ready();
        var result = new List<ICode>();
        foreach (var typeDict in Held.Providers.Values)
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
    public IEnumerable<ICode> All()
    {
        Ready();
        return Held.Providers.Values.SelectMany(dict => dict.Values);
    }

    public bool Has<T>() where T : class, ICode
    {
        Ready(typeof(T));
        return Held.Providers.TryGetValue(typeof(T), out var typeDict) && !typeDict.IsEmpty;
    }

    // --- Non-generic methods (single source of truth) ---

    /// <summary>
    /// Registers a provider by runtime-resolved type. First registered for a type becomes default.
    /// </summary>
    public data.@this Register(System.Type providerType, ICode provider)
    {
        Ready(providerType);
        var typeDict = Held.Providers.GetOrAdd(providerType, _ => new ConcurrentDictionary<string, ICode>(StringComparer.OrdinalIgnoreCase));

        if (!typeDict.TryAdd(provider.Name, provider))
            return Context.Error(new ActionError($"Provider '{provider.Name}' already registered for {providerType.Name}", "ProviderExists", 409));

        Held.Defaults.TryAdd(providerType, provider.Name);

        // A provider is engine plumbing, never a plang value — the registration
        // just succeeds. Returning the provider as the value would wrap a CLR
        // service in a Data (a throwaway clr); the value is never read.
        return Context.Ok();
    }

    /// <summary>
    /// Lists all providers registered for a runtime-resolved type (typed C# access).
    /// </summary>
    public IReadOnlyList<ICode> List(System.Type providerType)
    {
        Ready(providerType);
        return Held.Providers.TryGetValue(providerType, out var typeDict)
            ? typeDict.Values.ToList()
            : Array.Empty<ICode>();
    }

    /// <summary>
    /// Removes a named provider by runtime-resolved type. Cannot remove the default.
    /// </summary>
    public data.@this Remove(System.Type providerType, string name)
    {
        if (string.IsNullOrEmpty(name))
            return Context.Error(new ActionError("Provider name is required", "ValidationError", 400));

        Ready(providerType);
        if (!Held.Providers.TryGetValue(providerType, out var typeDict))
            return Context.Error(new ActionError($"Provider '{name}' not found", "ProviderNotFound", 404));

        if (!typeDict.TryGetValue(name, out var provider))
            return Context.Error(new ActionError($"Provider '{name}' not found", "ProviderNotFound", 404));

        if (Held.Defaults.TryGetValue(providerType, out var defaultName)
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

        Ready(providerType);
        if (!Held.Providers.TryGetValue(providerType, out var typeDict))
            return Context.Error(new ActionError($"Provider '{name}' not found", "ProviderNotFound", 404));

        if (!typeDict.TryGetValue(name, out var newDefault))
            return Context.Error(new ActionError($"Provider '{name}' not found", "ProviderNotFound", 404));

        // One slot per interface — the provider's other interfaces keep their own default.
        Held.Defaults[providerType] = newDefault.Name;
        return Context.Ok();
    }

    /// <summary>
    /// Registers built-in default providers. Called by App constructor.
    /// Each module owns its default provider — this method is the single registration point.
    /// </summary>
    public void RegisterDefaults() => _armed = true;

    // Whether this registry holds the built-ins (RegisterDefaults), and which are made, one bit each: an interface's
    // built-in is made the first time the registry is asked about that interface, all of them when it is asked about
    // every provider — an app that asks for one provider makes one.
    private bool _armed;
    private int _made;
    private static int Every => (1 << BuiltIn.Length) - 1;

    // The built-in providers, each module's default, by the interface it serves. Stamped built-in so the snapshot
    // capture skips them — a fresh App's RegisterDefaults reproduces the same set on Restore.
    private static readonly (System.Type Type, System.Func<@this, ICode> Make)[] BuiltIn =
    [
        // one ed25519 serves both signing and keys
        (typeof(ISigning), registry => registry.Signer),
        (typeof(IKey), registry => registry.Signer),
        (typeof(IIdentity), _ => new global::app.module.identity.code.Default()),
        (typeof(ICrypto), _ => new global::app.module.crypto.code.Default()),
        (typeof(global::app.module.math.code.IMath), _ => new global::app.module.math.code.Default()),
        (typeof(global::app.module.http.code.IHttp), _ => new global::app.module.http.code.Default()),
        (typeof(global::app.module.archive.code.IArchive), _ => new global::app.module.archive.code.Default()),
        (typeof(global::app.module.terminal.code.ITerminal), _ => new global::app.module.terminal.code.Default()),
        (typeof(global::app.module.browser.code.IBrowser), _ => new global::app.module.browser.code.Chromium()),
        // the screen: on Windows a window that shows frames; on Linux (PlangOS) the display that makes them
        (typeof(global::app.module.screen.code.IScreen), _ => OperatingSystem.IsWindows()
            ? new global::app.module.screen.code.Default()
            : new global::app.module.screen.code.Wayland()),
        (typeof(global::app.module.condition.code.IEvaluator), _ => new global::app.module.condition.code.Default()),
        (typeof(global::app.module.assert.code.IAssert), _ => new global::app.module.assert.code.Default()),
        (typeof(ITemplate), _ => new global::app.module.ui.code.Fluid()),
        (typeof(global::app.module.llm.code.ILlm), _ => new global::app.module.llm.code.OpenAi()),
        (typeof(global::app.module.llm.code.IDecider), _ => new global::app.module.llm.code.TypeSafe()),
        (typeof(global::app.module.build.code.IBuilder), _ => new global::app.module.build.code.Default()),
        (typeof(global::app.data.code.IGrep), _ => new global::app.data.code.Default()),
    ];

    private Ed25519? _signer;
    private Ed25519 Signer => _signer ??= new Ed25519();

    // Every built-in made — what a walk over all the providers sees.
    private void Ready()
    {
        if (!_armed || System.Threading.Volatile.Read(ref _made) == Every) return;
        lock (Held.Providers)
            for (var at = 0; at < BuiltIn.Length; at++) Make(at);
    }

    // The built-in of interface t made, when it has one.
    private void Ready(System.Type t)
    {
        if (!_armed || System.Threading.Volatile.Read(ref _made) == Every) return;
        for (var at = 0; at < BuiltIn.Length; at++)
            if (BuiltIn[at].Type == t)
                lock (Held.Providers) Make(at);
    }

    // Makes built-in `at` when it is not made (under the gate).
    private void Make(int at)
    {
        if ((_made & (1 << at)) != 0) return;
        var (type, make) = BuiltIn[at];
        var provider = make(this);
        provider.IsBuiltIn = true;
        var typeDict = Held.Providers.GetOrAdd(type, _ => new ConcurrentDictionary<string, ICode>(StringComparer.OrdinalIgnoreCase));
        typeDict.TryAdd(provider.Name, provider);
        Held.Defaults.TryAdd(type, provider.Name);
        // Stamp the type's "natural" default name *once* — the first built-in for a type
        // becomes default by virtue of being first-registered, mirroring Register's contract.
        Held.BuiltInDefaults.TryAdd(type, provider.Name);
        System.Threading.Volatile.Write(ref _made, _made | (1 << at));
    }
}
