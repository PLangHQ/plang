using app;
using app.module.setting;
using app.module.identity;
using app.module.identity.code;

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
    /// Per-actor permission view — signed grants on paths, keyed by verb
    /// + sub-options. <c>Find/Add/Revoke</c>. Routes "y" grants to an
    /// in-memory list (live for the App's lifetime) and "a" grants to the
    /// actor's permission setting, saved in the settings' store.
    /// </summary>
    public permission.@this Permission { get; private set; } = null!;

    private readonly global::app.channel.list.@this _channels;

    /// <summary>
    /// Named channels owned by this actor. Goal-channel recursion isolation lives
    /// on <see cref="channel.type.goal.@this.IsExecuting"/> — the registry's <c>Get</c>
    /// treats an executing goal-channel as not-found.
    /// </summary>
    public global::app.channel.list.@this Channel => _channels;

    private readonly global::app.task.list.@this _tasks;

    /// <summary>The tasks this actor runs — the goal calls made in parallel on it, each listed while it runs.</summary>
    public global::app.task.list.@this Task => _tasks;

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
    /// This actor's own identity. The system's is held by the identity store as the app's default once it is
    /// resolved (<c>%MyIdentity%</c>); a user's or a service's is set by the HTTP/signing layer.
    /// </summary>
    public Identity? Identity { get; set; }

    private readonly @this? _fallback;
    private global::app.actor.setting.@this? _setting;

    /// <summary>This actor's settings — the root its contexts' layers chain to; the user's falls back
    /// to the system's (<c>app.actor.list.System.Setting</c>).</summary>
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
        _channels = new global::app.channel.list.@this(app, this);
        _tasks = new global::app.task.list.@this(this);

        // Register %!app% — navigates the App object graph (e.g., %!app.goal.list%)
        Context.Variable.Set("!app", new data.DynamicData("!app", asker => asker.Ok(app), Context));

        // %MyIdentity% — the app's own identity; %Identity% — who this actor acts for: its identity's public key
        // (a caller's, in a service), else, in a local run, the app's own. Both computed on each read, so a
        // setDefault or rename is reflected.
        Context.Variable.Set("MyIdentity", new data.DynamicData("MyIdentity", asker => asker.Ok(DefaultIdentity), Context));
        Context.Variable.Set("Identity", new data.DynamicData("Identity", asker => asker.Ok((Identity ?? DefaultIdentity)?.PublicKey), Context));
    }

    /// <summary>The app's default identity — the system actor's, made on first ask. Not <see cref="Identity"/>'s
    /// fallback: the identity store reads the system's own slot while it makes the default.</summary>
    private Identity? DefaultIdentity
    {
        get
        {
            var (provider, _) = App.Code.Get<IIdentity>();
            if (provider == null) return null;
            // sync over async: a computed value answers synchronously on read
            var result = provider.GetOrCreateDefaultAsync(new global::app.module.identity.Get(App.actor.list.System.Context)).GetAwaiter().GetResult();
            return result.Success
                ? (result.Peek() as global::app.type.item.@this)?.Clr<Identity>() ?? result.Peek() as Identity
                : null;
        }
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
        // the settings' store, when this actor's settings are the chain's root that made it
        _setting?.Dispose();
    }
}
