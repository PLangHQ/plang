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
    global::app.type.item.IList<@this, global::app.actor.list.@this>, global::app.origin.@this
{
    private readonly CancellationTokenSource _cts;

    // what a program adds to the actor — kept as long as the actor lives
    private readonly global::app.type.item.kept.list.@this _kept = new();
    internal override global::app.type.item.kept.list.@this Kept => _kept;

    /// <summary>A key names this actor by its name — <c>system</c>, <c>user</c>; case is not the program's to get right.</summary>
    public ValueTask<@this?> Match(string key)
        => ValueTask.FromResult(string.Equals(Name, key, StringComparison.OrdinalIgnoreCase) ? this : null);

    /// <summary>The actor the asker acts as.</summary>
    public static @this? Current(global::app.actor.context.@this context) => context.Actor;

    /// <summary>The app's actors: System and User.</summary>
    public static global::app.actor.list.@this List(global::app.@this app) => new(app);

    /// <summary>
    /// Name of the actor ("System" or "User") — the name its role goes by.
    /// </summary>
    [global::app.LlmBuilder]
    public string Name => Role.Name;

    /// <summary>What this actor is in its app — the system or a user: its name, and what it stands on the app's
    /// folders.</summary>
    internal role.@this Role { get; }

    /// <summary>The key this actor's row for the setting at <paramref name="address"/> is kept under in the settings'
    /// store (<c>user!app.test.setting</c>).</summary>
    internal string Row(string address) => $"{Name.ToLowerInvariant()}!{address}";

    /// <summary>The setting address <paramref name="row"/> is this actor's row for; null when it is another actor's
    /// (every key of this actor's starts as its row for no address does).</summary>
    internal string? Address(string row)
        => Row("") is var prefix && row.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase) ? row[prefix.Length..] : null;

    /// <summary>An actor written alone, in every view, is its name — what a program reads an actor by; its context,
    /// channels and permissions are its own, never written with it.</summary>
    protected internal override System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        writer.String(Name);
        return default;
    }

    /// <summary>
    /// The PLang execution context owned by this actor.
    /// </summary>
    [global::app.LlmBuilder]
    public context.@this Context { get; }

    /// <summary>
    /// Per-actor permission view — signed grants on paths, keyed by verb
    /// + sub-options. <c>Find/Add/Revoke</c>. Routes "y" grants to an
    /// in-memory list (live for the App's lifetime) and "a" grants to the
    /// actor's permission setting, saved in the settings' store.
    /// </summary>
    public permission.@this Permission
        => _permission ?? System.Threading.Interlocked.CompareExchange(ref _permission, new permission.@this(this), null) ?? _permission;

    // the permission view, made when first asked; the first made is the one kept (it holds the grants)
    private permission.@this? _permission;

    private readonly global::app.channel.list.@this _channels;

    /// <summary>
    /// Named channels owned by this actor. Goal-channel recursion isolation lives
    /// on <see cref="channel.type.goal.@this.IsExecuting"/> — the registry's <c>Get</c>
    /// treats an executing goal-channel as not-found.
    /// </summary>
    [global::app.LlmBuilder]
    public global::app.channel.list.@this Channel => _channels;

    // the tasks, made when first asked; the first made is the one kept (it lists what runs)
    private global::app.task.list.@this? _tasks;

    /// <summary>The tasks this actor runs — the goal calls made in parallel on it, each listed while it runs.</summary>
    [global::app.LlmBuilder]
    public global::app.task.list.@this Task
        => _tasks ?? System.Threading.Interlocked.CompareExchange(ref _tasks, new global::app.task.list.@this(this), null) ?? _tasks;

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
    /// <param name="role">What the actor is in its app: the system or a user.</param>
    /// <param name="console">Whether this actor's channels begin as the console's standard ones, wired when first used.</param>
    internal @this(role.@this role, app.@this app, CancellationToken parentToken = default, @this? fallback = null, bool console = false)
        : base(app.type.list[typeof(@this)])
    {
        Role = role;
        App = app;
        _fallback = fallback;
        _cts = parentToken == default
            ? new CancellationTokenSource()
            : CancellationTokenSource.CreateLinkedTokenSource(parentToken);
        Context = new context.@this(app, this, parentToken: _cts.Token);
        _channels = new global::app.channel.list.@this(app, this, console);
        // %MyIdentity% and %Identity% are not set here: the memory is born holding them (variable.list), computed from
        // whoever asks, so an actor born at any time has them at once and no set — nor anything bound to one — runs.
    }

    /// <summary>The app's default identity — the system actor's, made on first ask. Not <see cref="Identity"/>'s
    /// fallback: the identity store reads the system's own slot while it makes the default.</summary>
    internal Identity? DefaultIdentity
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

    // As what a sub app is made from: the actor answers each part from its own app, the sub app's life ends with its
    // own, and the sub app is who it is.

    System.Threading.CancellationTokenSource global::app.origin.@this.Shutdown()
        => CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);

    // a sub app is made in its app's folder or under it, never in its app's stores folder or under it: rooted anywhere
    // else (an ancestor's, a sibling's, the stores'), what its app keeps there (its stores) would be plain in-root for it.
    // Its app's stores are the only ones to check down a chain of makers: its app was itself made in its own maker's
    // folder or under it — at that maker's root, its stores folder is the maker's; under it, the maker's stores lie
    // outside it.
    global::app.type.item.path.file.filesystem.@this global::app.origin.@this.Filesystem(string absolutePath)
    {
        var filesystem = new global::app.type.item.path.file.filesystem.@this(absolutePath);
        if (!filesystem.root.IsUnder(App.filesystem.root).Value)
            throw new global::app.error.AppException(new global::app.error.@this(
                $"a sub app is made in its maker's folder or under it: {filesystem.root.Absolute} is not in {App.filesystem.root.Absolute}",
                "OutsideItsMaker", 400));
        if (filesystem.root.IsUnder(App.filesystem.store).Value)
            throw new global::app.error.AppException(new global::app.error.@this(
                $"a sub app is never made in its maker's stores folder: {filesystem.root.Absolute} is in {App.filesystem.store.Absolute}",
                "OutsideItsMaker", 400));
        return filesystem;
    }

    global::app.type.list.@this global::app.origin.@this.type => App.type.list;

    global::app.module.@this global::app.origin.@this.Module(global::app.@this app) => App.module;

    // its app's goals are the sub app's while the sub app holds no code of its own (a goal is bound to its modules and
    // types when it is read, so a goal its app reads reaches only its app's) and shares its folder (a .pr is then the same
    // file in both)
    async Task<global::app.data.@this?> global::app.origin.@this.Goal(global::app.type.item.path.@this source, global::app.@this app)
        => app.HasCode || !Equals(app.filesystem.root, App.filesystem.root) ? null : await App.goal.list.Load(source);

    Identity? global::app.origin.@this.Identity(global::app.@this app) => Identity ?? DefaultIdentity;

    // a type of its app is not a sub app's to change, until sub apps own their code
    global::app.error.@this? global::app.origin.@this.Hold(global::app.type.kind.@this kind)
        => new($"'{kind.Name}' is a kind of {kind.Owner?.Name}, which this app shares with the app that made it: a sub app "
            + $"can't add a kind to {kind.Owner?.Name} yet", "TypesShared", 400);

    // a module of its app is not a sub app's to change, until sub apps own their code
    void global::app.origin.@this.Register(global::app.module.@this module, string action, System.Type type)
        => throw new global::app.error.AppException(new global::app.error.@this($"the module '{module.Name}' is shared with the "
            + $"app that made this one: a sub app adds its own modules, not actions to '{module.Name}'", "ModuleShared", 400));

    System.Threading.Tasks.ValueTask<bool> global::app.origin.@this.Remove(global::app.module.@this module, global::app.actor.context.@this context)
        => throw new global::app.error.AppException(new global::app.error.@this($"the module '{module.Name}' is shared with the "
            + $"app that made this one: a sub app takes out only its own modules", "ModuleShared", 400));

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
