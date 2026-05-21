using App;
using App.Settings;
using App.Variables;
using App.modules.identity;
using App.modules.identity.code;

namespace App.Actor;

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
    public Context.@this Context { get; }

    /// <summary>
    /// Per-actor permission view — signed grants on paths, keyed by verb
    /// + sub-options. <c>Find/Add/Revoke</c>. Routes "y" grants to an
    /// in-memory list (live for the App's lifetime) and "a" grants to
    /// <c>App.SettingsStore</c> under the <c>permission</c> table.
    /// </summary>
    public Permission.@this Permission { get; private set; } = null!;

    private readonly AppChannels _channels;
    private AppChannels? _foundationalChannels;
    private readonly AsyncLocal<AppChannels?> _channelsOverride = new();

    /// <summary>
    /// Named channels owned by this actor. Honours the AsyncLocal channel override
    /// (used by goal channels to isolate their execution from the live overlay).
    /// </summary>
    public AppChannels Channels => _channelsOverride.Value ?? _channels;

    /// <summary>
    /// The foundational (boot-time) channel set, used by goal channels to resolve
    /// writes against the original entry-point streams instead of the current overlay.
    /// Lazy: returns a snapshot at first access if <see cref="FreezeFoundational"/>
    /// has not been called yet.
    /// </summary>
    public AppChannels FoundationalChannels
    {
        get => _foundationalChannels ??= _channels.Snapshot();
        private set => _foundationalChannels = value;
    }

    /// <summary>
    /// Captures the current channel set as the foundational snapshot. Stage 6 wires
    /// PlangConsole to call this after registering all initial channels but before
    /// goal execution starts.
    /// </summary>
    public void FreezeFoundational() => _foundationalChannels = _channels.Snapshot();

    /// <summary>
    /// Pushes a channel override onto the AsyncLocal scope; disposing the returned
    /// scope restores the previous override. Used by Channel.Goal to make writes
    /// inside the goal resolve against the foundational set, not the live overlay.
    /// </summary>
    public IDisposable PushChannelsOverride(AppChannels overlay)
    {
        var prev = _channelsOverride.Value;
        _channelsOverride.Value = overlay;
        return new ChannelsOverrideScope(this, prev);
    }

    private sealed class ChannelsOverrideScope : IDisposable
    {
        private readonly @this _actor;
        private readonly AppChannels? _previous;
        public ChannelsOverrideScope(@this actor, AppChannels? previous)
        {
            _actor = actor;
            _previous = previous;
        }
        public void Dispose() => _actor._channelsOverride.Value = _previous;
    }

    /// <summary>
    /// Back-reference to the app.
    /// </summary>
    public App.@this App { get; }

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
    /// Closed list of actor names the LLM may emit for an Actor-typed slot.
    /// Build-time validation membership-checks against this; runtime resolves the
    /// chosen name via <see cref="Resolve"/> → <c>App.GetActor</c>.
    /// </summary>
    [App.Attributes.Choices]
    public static string[] Choices(Context.@this? ctx) => ["user", "system"];

    public @this(string name, App.@this app, CancellationToken parentToken = default)
    {
        Name = name;
        App = app;
        _cts = parentToken == default
            ? new CancellationTokenSource()
            : CancellationTokenSource.CreateLinkedTokenSource(parentToken);
        Context = new Context.@this(app, parentToken: _cts.Token);
        Context.Actor = this;
        Permission = new Permission.@this(this);
        _channels = new AppChannels(app) { Actor = this };

        // Register %Settings.X% as a navigable mount on this actor's Variables.
        // Resolution dispatches to app.Settings.Get(path, this.Context); the
        // lambda captures *this* actor's Context so per-actor ctx propagates.
        Context.Variables.RegisterNavigable("Settings", path => app.Settings.Get(path, Context));

        // Register %!app% — navigates the App object graph (e.g., %!app.Tester.IsEnabled%)
        Context.Variables.Set("!app", new Data.DynamicData("!app", () => app));

        // Register lazy %MyIdentity% — resolves to the System actor's default identity.
        // Data.DynamicData re-evaluates on each access, so changes via setDefault/rename are reflected.
        Context.Variables.Set("MyIdentity", new Data.DynamicData("MyIdentity", () =>
        {
            var provider = app.Code.Get<IIdentity>();
            if (!provider.Success) return null;
            var result = provider.Value!.GetOrCreateDefaultAsync(new Get { Context = app.System.Context }).GetAwaiter().GetResult();
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
