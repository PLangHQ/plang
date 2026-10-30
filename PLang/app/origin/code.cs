namespace app.origin;

/// <summary>
/// The code a root app is made from — its facts and the entries made from them: the types the scan finds (with path's
/// schemes), made at their first ask, and, born with the app it is made for, the actions. Each root app is made with a
/// code of its own, so nothing in it is another app's; what the app changes under its layers is its own. It has no
/// goals.
/// </summary>
internal sealed class code : @this
{
    /// <summary>The code, its types scanned from its facts on first ask, path's schemes among them; the app it is made
    /// for born wired as <paramref name="wiring"/> says.</summary>
    internal code(Wiring wiring) => _wiring = wiring;

    // The code's types, made at the first ask: the scan of its facts, then path's schemes, each a kind of path that
    // builds its own path subclass — added as one.
    private global::app.type.list.@this Types()
    {
        var types = new global::app.type.list.@this(this);
        var path = types[typeof(global::app.type.item.path.@this)];
        types.Add([
            new global::app.type.item.path.scheme.@this("file", path, (raw, context) => global::app.type.item.path.file.@this.Resolve(raw, context)),
            new global::app.type.item.path.scheme.@this("http", path, (raw, context) => global::app.type.item.path.http.@this.Resolve(raw, context)),
            new global::app.type.item.path.scheme.@this("https", path, (raw, context) => global::app.type.item.path.http.@this.Resolve(raw, context)),
        ]);
        return types;
    }

    public System.Threading.CancellationTokenSource Shutdown() => new();

    public global::app.type.item.path.file.filesystem.@this Filesystem(string absolutePath) => new(absolutePath);

    private readonly Wiring _wiring;

    // the one place the wiring is asked: the app's actors are born with the console's channels, or with none
    public bool Console => _wiring == Wiring.console;

    public bool Testing => false;

    public global::app.type.list.@this type
    {
        get
        {
            if (System.Threading.Volatile.Read(ref _type) is { } types) return types;
            lock (_making) return _type ??= Types();
        }
    }
    private global::app.type.list.@this? _type;
    private readonly object _making = new();

    // the actions, born with the app the code is made for: that app answers for them
    public global::app.module.@this Module(global::app.@this app) => new global::app.module.@this(app);

    public global::app.module.code.@this? Code => null;

    public System.Threading.Tasks.ValueTask<IEnumerable<global::app.@event.@this>> @event(global::app.actor.role.@this role) => new([]);

    public IEnumerable<global::app.type.item.path.@this> Events(global::app.@this app)
        => [app.filesystem.root.Combine("system").Combine("event"), app.filesystem.os.Combine("system").Combine("event")];

    public int Depth => 0;

    public System.Threading.Tasks.Task<global::app.data.@this?> Goal(global::app.type.item.path.@this source, global::app.@this app)
        => System.Threading.Tasks.Task.FromResult<global::app.data.@this?>(null);

    // the identity the System is making now, before its row is stored
    public global::app.module.identity.Identity? Identity(global::app.@this app) => app.actor.list.System.Identity;

    public global::app.error.@this? Hold(global::app.type.kind.@this kind)
    {
        type.Put(kind);
        return null;
    }

    public void Register(global::app.module.@this module, string action, System.Type type) => module.Add(action, type);

    public System.Threading.Tasks.ValueTask<global::app.data.@this<global::app.type.item.@bool.@this>> Remove(global::app.module.@this module, global::app.actor.context.@this context)
        => module.Root.Drop(module, context);
}
