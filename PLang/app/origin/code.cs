namespace app.origin;

/// <summary>
/// The code an app is made from — its facts and the entries made from them: the types the scan finds (with path's
/// schemes) and, born with the app it is made for, the actions. A new one for each app, so nothing in it is another
/// app's; what the app changes under its layers is its own. It has no goals.
/// </summary>
internal sealed class code : @this
{
    /// <summary>The code, its types scanned from its facts on first ask, path's schemes among them.</summary>
    internal code()
    {
        type = new global::app.type.list.@this(this);
        // path's schemes, each a kind of path that builds its own path subclass
        var path = type[typeof(global::app.type.item.path.@this)];
        type.Add(new global::app.type.item.path.scheme.@this("file", path, (raw, context) => global::app.type.item.path.file.@this.Resolve(raw, context)));
        type.Add(new global::app.type.item.path.scheme.@this("http", path, (raw, context) => global::app.type.item.path.http.@this.Resolve(raw, context)));
        type.Add(new global::app.type.item.path.scheme.@this("https", path, (raw, context) => global::app.type.item.path.http.@this.Resolve(raw, context)));
    }

    public System.Threading.CancellationTokenSource Shutdown() => new();

    public global::app.type.item.path.file.filesystem.@this Filesystem(string absolutePath) => new(absolutePath);

    public global::app.type.list.@this type { get; }

    // the actions, born with the app the code is made for: that app answers for them
    public global::app.module.@this Module(global::app.@this app) => new global::app.module.@this(app);

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

    public System.Threading.Tasks.ValueTask<bool> Remove(global::app.module.@this module, global::app.actor.context.@this context)
        => module.Drop(context);
}
