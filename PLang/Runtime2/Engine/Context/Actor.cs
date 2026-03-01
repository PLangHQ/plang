using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.DataSource;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.Engine.Context;

/// <summary>
/// Represents an actor in the system with its own context and IO channels.
/// </summary>
public sealed class Actor : IAsyncDisposable
{
    private readonly Lazy<IDataSource> _dataSource;

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
    public IDataSource DataSource => _dataSource.Value;

    /// <summary>
    /// Resolves an actor by name using the engine.
    /// Convention: types with this signature are auto-resolved by the source generator.
    /// </summary>
    public static Actor? Resolve(string name, Engine.@this engine) => engine.GetActor(name).Actor;

    /// <summary>
    /// Valid values for LLM action summaries.
    /// Convention: types with this property get their values shown in builder summaries.
    /// </summary>
    public static string[] ValidValues => ["user", "service", "system"];

    public Actor(string name, Engine.@this engine)
    {
        Name = name;
        Engine = engine;
        _dataSource = new Lazy<IDataSource>(CreateDataSource);
        Context = new PLangContext(engine)
        {
            CallStack = new CallStack.@this()
        };
        Context.Actor = this;
        Channels = new EngineChannels(engine);

        // Register shared SettingsData — same object for all actors.
        // %Settings.ApiKey% resolves identically in User, Service, and System contexts.
        Context.MemoryStack.Put(engine.SettingsVariable);
    }

    private IDataSource CreateDataSource()
    {
        if (Engine.Testing.IsEnabled || Engine.Building.IsEnabled)
            return SqliteDataSource.InMemory(Name.ToLowerInvariant());

        var dbDir = Engine.FileSystem.Path.Combine(Engine.AbsolutePath, ".db");
        var dbPath = Engine.FileSystem.Path.Combine(dbDir, $"{Name.ToLowerInvariant()}.sqlite");
        return new SqliteDataSource(dbPath, Engine.FileSystem);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dataSource.IsValueCreated)
            _dataSource.Value.Dispose();
        Context.Dispose();
        await Channels.DisposeAsync();
    }
}
