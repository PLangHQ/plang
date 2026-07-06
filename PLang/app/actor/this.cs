using app;
using app.module.setting;
using app.variable;
using app.module.identity;
using app.module.identity.code;

namespace app.actor;

/// <summary>
/// Represents an actor in the system with its own context and IO channels.
/// </summary>
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>, IAsyncDisposable
{
    /// <summary>
    /// OBP: actor is reached by NAME, not constructed. A name ("system"/"service"/
    /// "user") resolves to the App's existing actor — so `set channel … actor "system"`
    /// converts the name to the live actor. Self-owns conversion (OwnerOf discovers
    /// any type with a Convert hook). Born-native: the name arrives as text — unwrap.
    /// </summary>
    public static global::app.data.@this Convert(object? value, string? kind,
        global::app.actor.context.@this context)
    {
        if (value is global::app.type.text.@this t) value = t.Clr<string>();
        switch (value)
        {
            case null: return context.Ok(value);
            case @this self: return context.Ok(self);
            case string name:
                return context.Ok(context.App.GetActor(name));
            default:
                return context.Error(new global::app.error.Error(
                    $"Cannot convert {value.GetType().Name} to actor — expected an actor name (system/service/user).",
                    "ActorConversionFailed", 400));
        }
    }

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

    private readonly global::app.channel.list.@this _channels;

    /// <summary>
    /// Named channels owned by this actor. Goal-channel recursion isolation lives
    /// on <see cref="channel.type.goal.@this.IsExecuting"/> — the registry's <c>Get</c>
    /// treats an executing goal-channel as not-found.
    /// </summary>
    public global::app.channel.list.@this Channel => _channels;

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
    public static @this? Resolve(string name, context.@this context) => context.App.GetActor(name);

    /// <summary>
    /// Closed list of actor names the LLM may emit for an Actor-typed slot.
    /// Build-time validation membership-checks against this; runtime resolves the
    /// chosen name via <see cref="Resolve"/> → <c>App.GetActor</c>.
    /// </summary>
    [app.Attributes.Choices]
    public static string[] Choices(context.@this? context) => ["user", "system"];

    public @this(string name, app.@this app, CancellationToken parentToken = default)
    {
        Name = name;
        App = app;
        _cts = parentToken == default
            ? new CancellationTokenSource()
            : CancellationTokenSource.CreateLinkedTokenSource(parentToken);
        Context = new context.@this(app, this, parentToken: _cts.Token);
        Permission = new permission.@this(this);
        // Per-Actor Serializers: bound to this actor's Context so PathJsonConverter
        // produces Context-wired Paths on deserialize without any ambient state.
        _channels = new global::app.channel.list.@this(app, new global::app.channel.serializer.list.@this(Context)) { Actor = this };

        // Register %setting.X% as a navigable mount on this actor's Variables.
        // Resolution dispatches to the persistent side of app.Setting (sqlite).
        Context.Variable.RegisterNavigable("setting", path => app.Setting.Get(global::app.setting.Storage.Persistent, path));

        // Register %!app% — navigates the App object graph (e.g., %!app.test.IsEnabled%)
        Context.Variable.Set("!app", new data.DynamicData("!app", () => app, Context));

        // Register lazy %MyIdentity% — resolves to the System actor's default identity.
        // Data.DynamicData re-evaluates on each access, so changes via setDefault/rename are reflected.
        Context.Variable.Set("MyIdentity", new data.DynamicData("MyIdentity", () =>
        {
            var (idProvider, _) = app.Code.Get<IIdentity>();
            if (idProvider == null) return null;
            var result = idProvider.GetOrCreateDefaultAsync(new global::app.module.identity.Get(app.System.Context)).GetAwaiter().GetResult();
            return result.Success ? global::app.type.item.@this.Lower<Identity>(result.Peek()) : null;
        }, Context));
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
