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

    /// <summary>The registry of <paramref name="context"/>'s app, holding the built-in providers — registered at its
    /// first use, so an app that asks for none makes none.</summary>
    public @this(actor.context.@this context) => Context = context;

    // The registry's table, made at its first use, knowing what the code ships for each interface.
    private sealed class table
    {
        internal readonly ConcurrentDictionary<System.Type, ConcurrentDictionary<string, ICode>> Providers = new();
        // The default's name per interface. A default is selection, and selection is the registry's: one
        // provider can serve several interfaces (Ed25519 is ISigning and IKey), each with its own default.
        internal readonly ConcurrentDictionary<System.Type, string> Defaults = new();
        // The default each interface was born with. Snapshot capture compares the current default against it to
        // tell a user's selection from what a fresh App sets itself.
        internal readonly ConcurrentDictionary<System.Type, string> BuiltIn = new();
        // What the code ships for each interface, made when the interface is first asked for; one provider can be
        // shipped for several (Ed25519), made once.
        internal readonly Dictionary<System.Type, Lazy<ICode>> Shipped = new();

        // The providers of `type`, the one the code ships for it in first: made the first time anything asks for the
        // interface, so it is the first there, the default, whatever is registered after. Null for an interface
        // nothing ships and nothing registered.
        internal ConcurrentDictionary<string, ICode>? Of(System.Type type)
        {
            if (Providers.TryGetValue(type, out var made)) return made;
            if (!Shipped.TryGetValue(type, out var shipped)) return null;
            return Providers.GetOrAdd(type, _ =>
            {
                var provider = shipped.Value;
                provider.IsBuiltIn = true;
                Defaults.TryAdd(type, provider.Name);
                // the type's "natural" default name, once — the shipped provider is its default by being first
                BuiltIn.TryAdd(type, provider.Name);
                return new ConcurrentDictionary<string, ICode>(StringComparer.OrdinalIgnoreCase) { [provider.Name] = provider };
            });
        }
    }

    private table? _table;
    private readonly object _gate = new();

    // The table as it is now: made, knowing what the code ships, on the first use — the one door every other reads
    // through.
    private table Table
    {
        get
        {
            if (System.Threading.Volatile.Read(ref _table) is { } table) return table;
            lock (_gate)
            {
                if (_table is { } again) return again;
                table = new table();
                Ship(table);
                System.Threading.Volatile.Write(ref _table, table);
                return table;
            }
        }
    }

    /// <summary>
    /// Disposes every registered provider instance (IAsyncDisposable preferred,
    /// IDisposable fallback). Same projection as <see cref="All"/>.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        // a registry never used made no provider, so it has none to dispose
        if (System.Threading.Volatile.Read(ref _table) is not { } table) return;
        foreach (var provider in table.Providers.Values.SelectMany(p => p.Values))
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
        if (Table.Of(typeof(T)) is not { } typeDict)
            return (null, new ActionError($"No {typeof(T).Name} provider registered", "ProviderNotFound", 404));

        if (!string.IsNullOrEmpty(name))
        {
            if (!typeDict.TryGetValue(name, out var provider))
                return (null, new ActionError($"Provider '{name}' not found for {typeof(T).Name}", "ProviderNotFound", 404));
            return ((T)provider, null);
        }

        if (Table.Defaults.TryGetValue(typeof(T), out var defaultName) && typeDict.TryGetValue(defaultName, out var chosen))
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
        => Table.Of(typeof(T)) is { } typeDict ? typeDict.Values.Cast<T>().ToList() : Array.Empty<T>();

    /// <summary>
    /// Lists all providers across all types — every one the code ships among them, so each is made.
    /// </summary>
    public IReadOnlyList<ICode> List()
    {
        var table = Table;
        foreach (var type in table.Shipped.Keys) table.Of(type);
        var result = new List<ICode>();
        foreach (var typeDict in table.Providers.Values)
            result.AddRange(typeDict.Values);
        return result;
    }

    /// <summary>
    /// Iterates the provider instances made so far, across all types: one the code ships and nothing asked for is
    /// none of them. Used for disposal and inspection.
    /// </summary>
    public IEnumerable<ICode> All() => (System.Threading.Volatile.Read(ref _table)?.Providers.Values ?? [])
        .SelectMany(dict => dict.Values);

    /// <summary>
    /// Checks if any provider is registered for the given type.
    /// </summary>
    /// <remarks>Answered from what the code ships and what was made, making nothing.</remarks>
    public bool Has<T>() where T : class, ICode
        => Table.Shipped.ContainsKey(typeof(T)) || Table.Providers.TryGetValue(typeof(T), out var typeDict) && !typeDict.IsEmpty;

    // --- Non-generic methods (single source of truth) ---

    /// <summary>
    /// Registers a provider by runtime-resolved type. First registered for a type becomes default.
    /// </summary>
    public data.@this Register(System.Type providerType, ICode provider)
        // A provider is engine plumbing, never a plang value — the registration
        // just succeeds. Returning the provider as the value would wrap a CLR
        // service in a Data (a throwaway clr); the value is never read.
        => Put(Table, providerType, provider) ? Context.Ok()
            : Context.Error(new ActionError($"Provider '{provider.Name}' already registered for {providerType.Name}", "ProviderExists", 409));

    // Registers provider for providerType in table; the first for a type becomes its default. False when a provider
    // of its name is already there.
    private bool Put(table table, System.Type providerType, ICode provider)
    {
        var typeDict = table.Of(providerType)
            ?? table.Providers.GetOrAdd(providerType, _ => new ConcurrentDictionary<string, ICode>(StringComparer.OrdinalIgnoreCase));
        if (!typeDict.TryAdd(provider.Name, provider)) return false;
        table.Defaults.TryAdd(providerType, provider.Name);
        return true;
    }

    /// <summary>
    /// Lists all providers registered for a runtime-resolved type (typed C# access).
    /// </summary>
    public IReadOnlyList<ICode> List(System.Type providerType)
        => Table.Of(providerType) is { } typeDict ? typeDict.Values.ToList() : Array.Empty<ICode>();

    /// <summary>
    /// Removes a named provider by runtime-resolved type. Cannot remove the default.
    /// </summary>
    public data.@this Remove(System.Type providerType, string name)
    {
        if (string.IsNullOrEmpty(name))
            return Context.Error(new ActionError("Provider name is required", "ValidationError", 400));

        if (Table.Of(providerType) is not { } typeDict)
            return Context.Error(new ActionError($"Provider '{name}' not found", "ProviderNotFound", 404));

        if (!typeDict.TryGetValue(name, out var provider))
            return Context.Error(new ActionError($"Provider '{name}' not found", "ProviderNotFound", 404));

        if (Table.Defaults.TryGetValue(providerType, out var defaultName)
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

        if (Table.Of(providerType) is not { } typeDict)
            return Context.Error(new ActionError($"Provider '{name}' not found", "ProviderNotFound", 404));

        if (!typeDict.TryGetValue(name, out var newDefault))
            return Context.Error(new ActionError($"Provider '{name}' not found", "ProviderNotFound", 404));

        // One slot per interface — the provider's other interfaces keep their own default.
        Table.Defaults[providerType] = newDefault.Name;
        return Context.Ok();
    }

    // What the code ships for each interface, into the table as it is made — each provider made the first time its
    // interface is asked for. Each module owns its default provider — this is the single registration point. Every
    // built-in is stamped so the snapshot capture skips them: a fresh App's registry holds the same set, so they don't
    // ride along in the payload.
    private void Ship(table table)
    {
        var ed25519 = new Lazy<ICode>(() => new Ed25519());
        Ship<ISigning>(table, ed25519);
        Ship<IKey>(table, ed25519);
        Ship<IIdentity>(table, new(() => new global::app.module.identity.code.Default()));
        Ship<ICrypto>(table, new(() => new global::app.module.crypto.code.Default()));
        Ship<global::app.module.math.code.IMath>(table, new(() => new global::app.module.math.code.Default()));
        Ship<global::app.module.http.code.IHttp>(table, new(() => new global::app.module.http.code.Default()));
        Ship<global::app.module.archive.code.IArchive>(table, new(() => new global::app.module.archive.code.Default()));
        Ship<global::app.module.terminal.code.ITerminal>(table, new(() => new global::app.module.terminal.code.Default()));
        Ship<global::app.module.browser.code.IBrowser>(table, new(() => new global::app.module.browser.code.Chromium()));
        // the screen: on Windows a window that shows frames; on Linux (PlangOS) the display that makes them
        Ship<global::app.module.screen.code.IScreen>(table, new(() => OperatingSystem.IsWindows()
            ? new global::app.module.screen.code.Default()
            : new global::app.module.screen.code.Wayland()));
        Ship<global::app.module.condition.code.IEvaluator>(table, new(() => new global::app.module.condition.code.Default()));
        Ship<global::app.module.assert.code.IAssert>(table, new(() => new global::app.module.assert.code.Default()));
        Ship<ITemplate>(table, new(() => new global::app.module.ui.code.Fluid()));
        Ship<global::app.module.llm.code.ILlm>(table, new(() => new global::app.module.llm.code.OpenAi()));
        Ship<global::app.module.llm.code.IAnswer>(table, new(() => new global::app.module.llm.code.Live()));
        Ship<global::app.module.llm.code.IDecider>(table, new(() => new global::app.module.llm.code.TypeSafe()));
        Ship<global::app.module.build.code.IBuilder>(table, new(() => new global::app.module.build.code.Default()));
        Ship<global::app.data.code.IGrep>(table, new(() => new global::app.data.code.Default()));
        Ship<global::app.store.code.IStore>(table, new(() => new global::app.store.code.Sqlite()));
    }

    private void Ship<T>(table table, Lazy<ICode> provider) where T : class, ICode => table.Shipped[typeof(T)] = provider;
}
