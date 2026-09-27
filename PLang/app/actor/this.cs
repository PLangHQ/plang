using app;
using app.module.action.setting;
using app.module.action.identity;
using app.module.action.identity.code;

namespace app.actor;

/// <summary>
/// Represents an actor in the system with its own context and IO channels.
/// </summary>
[global::app.Attributes.PlangType("actor")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>, IAsyncDisposable,
    global::app.type.item.IMatch<@this>, global::app.type.item.ICurrent<@this>, global::app.type.item.ILoad<@this>,
    global::app.type.item.IList<@this, global::app.actor.list.@this>
{
    private readonly CancellationTokenSource _cts;

    /// <summary>A key names this actor by its name — <c>system</c>, <c>user</c>; case is not the program's to get right.</summary>
    public ValueTask<@this?> Match(string key)
        => ValueTask.FromResult(string.Equals(Name, key, StringComparison.OrdinalIgnoreCase) ? this : null);

    /// <summary>The actor the asker acts as.</summary>
    public static @this? Current(global::app.actor.context.@this context) => context.Actor;

    /// <summary>The app's actors: System and User.</summary>
    public static global::app.actor.list.@this List(global::app.@this app) => new(app);

    /// <summary>
    /// Name of the actor ("System", "Service", or "User").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The PLang execution context owned by this actor.
    /// </summary>
    public context.@this Context { get; }

    /// <summary>
    /// This actor's call tree. Each actor owns its own — the actor is the isolation
    /// unit (Variables, Events, Channels are all per-actor), and the call stack is no
    /// exception: a cross-actor call starts a separate tree, matching the actor model
    /// (in Erlang, A calling B doesn't graft B's stack under A's). Fork-safe within the
    /// actor's own flows via the AsyncLocal Current — that isolation is about parallel
    /// Task branches, orthogonal to actor identity.
    /// </summary>
    public global::app.callstack.@this CallStack { get; } = new();

    /// <summary>
    /// Per-actor permission view — signed grants on paths, keyed by verb
    /// + sub-options. <c>Find/Add/Revoke</c>. Routes "y" grants to an
    /// in-memory list (live for the App's lifetime) and "a" grants to
    /// <c>app.store</c> under the <c>permission</c> table.
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

    private readonly @this? _fallback;
    private global::app.actor.setting.@this? _setting;

    /// <summary>This actor's settings — the root its contexts' layers chain to; the user's falls back
    /// to the system's (<c>app.System.Setting</c>).</summary>
    public global::app.actor.setting.@this Setting => _setting ??= new(this, _fallback?.Setting);

    /// <param name="fallback">The actor whose settings answer what this one's don't — the system, for the user.</param>
    public @this(string name, app.@this app, CancellationToken parentToken = default, @this? fallback = null)
    {
        Name = name;
        App = app;
        _fallback = fallback;
        _cts = parentToken == default
            ? new CancellationTokenSource()
            : CancellationTokenSource.CreateLinkedTokenSource(parentToken);
        Context = new context.@this(app, this, parentToken: _cts.Token);
        Permission = new permission.@this(this);
        // Per-Actor Serializers: bound to this actor's Context so PathJsonConverter
        // produces Context-wired Paths on deserialize without any ambient state.
        _channels = new global::app.channel.list.@this(app, new global::app.channel.serializer.list.@this(Context)) { Actor = this };

        // Register %!app% — navigates the App object graph (e.g., %!app.test.Verbose%)
        Context.Variable.Set("!app", new data.DynamicData("!app", () => app, Context));

        // Register lazy %MyIdentity% — resolves to the System actor's default identity.
        // Data.DynamicData re-evaluates on each access, so changes via setDefault/rename are reflected.
        Context.Variable.Set("MyIdentity", new data.DynamicData("MyIdentity", () =>
        {
            var (idProvider, _) = app.Code.Get<IIdentity>();
            if (idProvider == null) return null;
            var result = idProvider.GetOrCreateDefaultAsync(new global::app.module.action.identity.Get(app.System.Context)).GetAwaiter().GetResult();
            return result.Success
                ? (result.Peek() as global::app.type.item.@this)?.Clr<Identity>() ?? result.Peek() as Identity
                : null;
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
