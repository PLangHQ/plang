using App;
using App.Settings;
using App.Variables;
using App.modules.identity;
using App.modules.identity.providers;

namespace App.Context;

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
    public Context.@this Context { get; }

    /// <summary>
    /// Named channels owned by this actor.
    /// </summary>
    public AppChannels Channels { get; }

    /// <summary>
    /// Back-reference to the app.
    /// </summary>
    public App.@this App { get; }

    /// <summary>
    /// Persistent key-value storage for this actor.
    /// Created lazily on first access. Database stored at .db/{actorname}.sqlite.
    /// </summary>
    public ISettingsStore SettingsStore => _dataSource.Value;

    /// <summary>
    /// Identity for this actor.
    /// For System actor: resolved via Data.DynamicData %MyIdentity% on first access.
    /// For User/Service: set externally by HTTP/signing layer.
    /// </summary>
    public Identity? Identity { get; set; }

    /// <summary>
    /// Resolves an actor by name using the app.
    /// Convention: types with this signature are auto-resolved by the source generator.
    /// </summary>
    public static Actor? Resolve(string name, Context.@this context) => context.App.GetActor(name).Actor;

    /// <summary>
    /// Valid values for LLM action summaries.
    /// Convention: types with this property get their values shown in builder summaries.
    /// </summary>
    public static string[] ValidValues => ["user", "service", "system"];

    /// <summary>
    /// Escalation level. System (2) can execute as anyone. User/Service (1) only as themselves.
    /// </summary>
    public int EscalationLevel => Name.ToLowerInvariant() switch
    {
        "system" => 2, "service" => 1, "user" => 1, _ => 0
    };

    public Actor(string name, App.@this app)
    {
        Name = name;
        App = app;
        _dataSource = new Lazy<ISettingsStore>(CreateSettingsStore);
        Context = new Context.@this(app)
        {
            CallStack = new CallStack.@this()
        };
        Context.Actor = this;
        Channels = new AppChannels(app);

        // Register shared SettingsVariable — same object for all actors.
        // %Settings.ApiKey% resolves identically in User, Service, and System contexts.
        Context.Variables.Put(app.SettingsVariable);

        // Register lazy %MyIdentity% — resolves to the System actor's default identity.
        // Data.DynamicData re-evaluates on each access, so changes via setDefault/rename are reflected.
        Context.Variables.Put(new Data.DynamicData("MyIdentity", () =>
        {
            var provider = app.Providers.Get<IIdentityProvider>();
            if (!provider.Success) return null;
            var identity = provider.Value!.GetOrCreateDefaultAsync(new Get { Context = app.Context }).GetAwaiter().GetResult();
            return identity.Success ? identity : null;
        }));
    }

    private ISettingsStore CreateSettingsStore()
    {
        // System actor always uses on-disk — it holds the LLM cache and other
        // persistent system data that must survive across app instances.
        // User/Service actors use in-memory during building/testing for isolation.
        if ((App.Testing.IsEnabled || App.Building.IsEnabled)
            && !Name.Equals("System", StringComparison.OrdinalIgnoreCase))
            return SqliteSettingsStore.InMemory(Name.ToLowerInvariant());

        var dbDir = App.FileSystem.Path.Combine(App.AbsolutePath, ".db");
        var dbPath = App.FileSystem.Path.Combine(dbDir, $"{Name.ToLowerInvariant()}.sqlite");
        return new SqliteSettingsStore(dbPath, App.FileSystem);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dataSource.IsValueCreated)
            _dataSource.Value.Dispose();
        Context.Dispose();
        await Channels.DisposeAsync();
    }
}
