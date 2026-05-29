using app;
using app.modules.settings;
using app.variable;
using app.modules.identity;
using app.modules.identity.code;

namespace app.actor;

/// <summary>
/// Represents an actor in the system with its own context and IO channels.
/// </summary>
public sealed class @this : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts;

    /// <summary>
    /// Name of the actor ("System", "Service", or "User").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The PLang execution context owned by this actor.
    /// </summary>
    public context.@this Context { get; }

    /// <summary>
    /// Per-actor permission view — signed grants on paths, keyed by verb
    /// + sub-options. <c>Find/Add/Revoke</c>. Routes "y" grants to an
    /// in-memory list (live for the App's lifetime) and "a" grants to
    /// <c>App.SettingsStore</c> under the <c>permission</c> table.
    /// </summary>
    public permission.@this Permission { get; private set; } = null!;

    private readonly AppChannels _channels;

    /// <summary>
    /// Named channels owned by this actor. Goal-channel recursion isolation lives
    /// on <see cref="channel.goal.@this.IsExecuting"/> — the registry's <c>Get</c>
    /// treats an executing goal-channel as not-found.
    /// </summary>
    public AppChannels Channels => _channels;

    /// <summary>
    /// Back-reference to the app.
    /// </summary>
    public app.@this App { get; }

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
    public static @this? Resolve(string name, context.@this ctx) => ctx.App.GetActor(name).Actor;

    /// <summary>
    /// Closed list of actor names the LLM may emit for an Actor-typed slot.
    /// Build-time validation membership-checks against this; runtime resolves the
    /// chosen name via <see cref="Resolve"/> → <c>App.GetActor</c>.
    /// </summary>
    [app.Attributes.Choices]
    public static string[] Choices(context.@this? ctx) => ["user", "system"];

    public @this(string name, app.@this app, CancellationToken parentToken = default)
    {
        Name = name;
        App = app;
        _cts = parentToken == default
            ? new CancellationTokenSource()
            : CancellationTokenSource.CreateLinkedTokenSource(parentToken);
        Context = new context.@this(app, parentToken: _cts.Token);
        Context.Actor = this;
        Permission = new permission.@this(this);
        // Per-Actor Serializers: bound to this actor's Context so PathJsonConverter
        // produces Context-wired Paths on deserialize without any ambient state.
        _channels = new AppChannels(app, new global::app.channel.serializer.list.@this(Context)) { Actor = this };

        // Register %Settings.X% as a navigable mount on this actor's Variables.
        // Resolution dispatches to app.Settings.Get(path, this.Context); the
        // lambda captures *this* actor's Context so per-actor ctx propagates.
        Context.Variables.RegisterNavigable("Settings", path => app.Settings.Get(path, Context));

        // Register %!app% — navigates the App object graph (e.g., %!app.tester.IsEnabled%)
        Context.Variables.Set("!app", new data.DynamicData("!app", () => app));

        // Register lazy %MyIdentity% — resolves to the System actor's default identity.
        // Data.DynamicData re-evaluates on each access, so changes via setDefault/rename are reflected.
        Context.Variables.Set("MyIdentity", new data.DynamicData("MyIdentity", () =>
        {
            var provider = app.Code.Get<IIdentity>();
            if (!provider.Success) return null;
            var result = provider.Value!.GetOrCreateDefaultAsync(new global::app.modules.identity.Get { Context = app.System.Context }).GetAwaiter().GetResult();
            return result.Success ? result.Value as Identity : null;
        }));
    }

    /// <summary>
    /// Cancels this actor. If System, cascades to User and Service.
    /// </summary>
    public void Cancel() => _cts.Cancel();

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _cts.Dispose();
        Context.Dispose();
        await _channels.DisposeAsync();
    }
}
