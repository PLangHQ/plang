using App;
using App.Settings;
using App.Variables;
using App.modules.identity;
using App.modules.identity.providers;

namespace App.Actor;

/// <summary>
/// Represents an actor in the system with its own context and IO channels.
/// </summary>
public sealed class @this : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts;
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
    /// Cancellation token for this actor. Cancel this to stop the actor and all its children.
    /// System is the root — cancelling System cascades to User and Service.
    /// User/Service can be cancelled independently.
    /// </summary>
    public CancellationToken CancellationToken => _cts.Token;

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
    public static @this? Resolve(string name, Context.@this context) => context.App.GetActor(name).Actor;

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

    public @this(string name, App.@this app, CancellationToken parentToken = default)
    {
        Name = name;
        App = app;
        _cts = parentToken == default
            ? new CancellationTokenSource()
            : CancellationTokenSource.CreateLinkedTokenSource(parentToken);
        _dataSource = new Lazy<ISettingsStore>(CreateSettingsStore);
        Context = new Context.@this(app, parentToken: _cts.Token);
        Context.Actor = this;
        Channels = new AppChannels(app);

        // Register shared SettingsVariable — same object for all actors.
        // %Settings.ApiKey% resolves identically in User, Service, and System contexts.
        Context.Variables.Set(app.SettingsVariable.Name, app.SettingsVariable);

        // Register %!app% — navigates the App object graph (e.g., %!app.Testing.IsEnabled%)
        Context.Variables.Set("!app", new Data.DynamicData("!app", () => app));

        // Register lazy %MyIdentity% — resolves to the System actor's default identity.
        // Data.DynamicData re-evaluates on each access, so changes via setDefault/rename are reflected.
        Context.Variables.Set("MyIdentity", new Data.DynamicData("MyIdentity", () =>
        {
            var provider = app.Providers.Get<IIdentityProvider>();
            if (!provider.Success) return null;
            var result = provider.Value!.GetOrCreateDefaultAsync(new Get { Context = app.Context }).GetAwaiter().GetResult();
            return result.Success ? result.Value as Identity : null;
        }));
    }

    private ISettingsStore CreateSettingsStore()
    {
        // Testing: every actor (including System) gets an in-memory db scoped by App.Id
        // so per-test Apps never share state. SQLite's shared-cache merges in-memory dbs
        // with identical DataSource names, so the App.Id scoping is load-bearing.
        if (App.Testing.IsEnabled)
            return SqliteSettingsStore.InMemory($"{Name.ToLowerInvariant()}-{App.Id}");

        // Building: User/Service in-memory (isolation); System on-disk so the LLM cache
        // and other persistent system data survive across build invocations.
        if (App.Build.IsEnabled && this != App.System)
            return SqliteSettingsStore.InMemory($"{Name.ToLowerInvariant()}-{App.Id}");

        var dbDir = App.FileSystem.Path.Combine(App.AbsolutePath, ".db");
        var dbPath = App.FileSystem.Path.Combine(dbDir, $"{Name.ToLowerInvariant()}.sqlite");
        return new SqliteSettingsStore(dbPath, App.FileSystem);
    }

    /// <summary>
    /// Cancels this actor. If System, cascades to User and Service.
    /// </summary>
    public void Cancel() => _cts.Cancel();

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _cts.Dispose();
        if (_dataSource.IsValueCreated)
            _dataSource.Value.Dispose();
        Context.Dispose();
        await Channels.DisposeAsync();
    }
}
