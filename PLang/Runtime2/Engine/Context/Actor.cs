using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Settings;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.identity;
using PLang.Runtime2.modules.identity.providers;

namespace PLang.Runtime2.Engine.Context;

/// <summary>
/// Represents an actor in the system with its own context and IO channels.
/// </summary>
public sealed class Actor : IAsyncDisposable
{
    private readonly Lazy<ISettingsStore> _dataSource;

    /// <summary>
    /// Name of the actor ("System", "Service", or "User").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The PLang execution context owned by this actor.
    /// </summary>
    public PLangContext Context { get; }

    /// <summary>
    /// Named channels owned by this actor.
    /// </summary>
    public EngineChannels Channels { get; }

    /// <summary>
    /// Back-reference to the engine.
    /// </summary>
    public Engine.@this Engine { get; }

    /// <summary>
    /// Persistent key-value storage for this actor.
    /// Created lazily on first access. Database stored at .db/{actorname}.sqlite.
    /// </summary>
    public ISettingsStore SettingsStore => _dataSource.Value;

    /// <summary>
    /// Identity for this actor.
    /// For System actor: resolved via DynamicData %MyIdentity% on first access.
    /// For User/Service: set externally by HTTP/signing layer.
    /// </summary>
    public IdentityData? Identity { get; set; }

    /// <summary>
    /// Resolves an actor by name using the engine.
    /// Convention: types with this signature are auto-resolved by the source generator.
    /// </summary>
    public static Actor? Resolve(string name, PLangContext context) => context.Engine.GetActor(name).Actor;

    /// <summary>
    /// Valid values for LLM action summaries.
    /// Convention: types with this property get their values shown in builder summaries.
    /// </summary>
    public static string[] ValidValues => ["user", "service", "system"];

    public Actor(string name, Engine.@this engine)
    {
        Name = name;
        Engine = engine;
        _dataSource = new Lazy<ISettingsStore>(CreateSettingsStore);
        Context = new PLangContext(engine)
        {
            CallStack = new CallStack.@this()
        };
        Context.Actor = this;
        Channels = new EngineChannels(engine);

        // Register shared SettingsData — same object for all actors.
        // %Settings.ApiKey% resolves identically in User, Service, and System contexts.
        Context.MemoryStack.Put(engine.SettingsVariable);

        // Register lazy %MyIdentity% — resolves to the System actor's default identity.
        // DynamicData re-evaluates on each access, so changes via setDefault/rename are reflected.
        Context.MemoryStack.Put(new DynamicData("MyIdentity", () =>
        {
            var provider = engine.Providers.Get<IIdentityProvider>();
            if (!provider.Success) return null;
            var identity = provider.Value!.GetOrCreateDefaultAsync(new Get { Context = engine.Context }).GetAwaiter().GetResult();
            return identity.Success ? identity : null;
        }));
    }

    private ISettingsStore CreateSettingsStore()
    {
        if (Engine.Testing.IsEnabled || Engine.Building.IsEnabled)
            return SqliteSettingsStore.InMemory(Name.ToLowerInvariant());

        var dbDir = Engine.FileSystem.Path.Combine(Engine.AbsolutePath, ".db");
        var dbPath = Engine.FileSystem.Path.Combine(dbDir, $"{Name.ToLowerInvariant()}.sqlite");
        return new SqliteSettingsStore(dbPath, Engine.FileSystem);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dataSource.IsValueCreated)
            _dataSource.Value.Dispose();
        Context.Dispose();
        await Channels.DisposeAsync();
    }
}
