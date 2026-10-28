namespace app.snapshot;

/// <summary>
/// Snapshot — navigation. A snapshot holds its app, so navigating and editing it is the app's:
/// <c>%snap.app.actor.list.user.context.variable.row.x%</c> reads the user's saved <c>x</c>, and
/// <c>set %snap.app.….x% = 2</c> edits it. The snapshot knows nothing of what each object keeps; each reads its own back
/// on a resume.
/// </summary>
public sealed partial class @this
{
    /// <summary>A child read — its app: what an app wrote is opened once, the first time it is navigated, and kept open,
    /// so an edit made through it is the snapshot's.</summary>
    public override System.Threading.Tasks.ValueTask<data.@this> Get(data.@this parent, string key)
    {
        if (_app == null || !string.Equals(key, "app", System.StringComparison.OrdinalIgnoreCase)) return new(parent.Context.NotFound(key));
        if (_app is global::app.type.item.source unread
            && View.Make(parent.Context.App.type.list[typeof(global::app.type.item.dict.@this)], unread, parent.Context) is { } opened)
            _app = opened;
        return new(new data.@this(key, _app, parent: parent));
    }

    /// <summary>A child write — its app, replaced; a snapshot holds nothing else.</summary>
    public override System.Threading.Tasks.ValueTask<global::app.data.@this> Set(string key, bool isIndex, object? value, global::app.actor.context.@this context)
    {
        if (!string.Equals(key, "app", System.StringComparison.OrdinalIgnoreCase) || value is not global::app.type.item.@this app)
            return new(context.Error(new global::app.error.@this(
                $"a snapshot holds its app, not '{key}'", "SnapshotHoldsTheApp", 400)));
        _app = app;
        return new(context.Ok(this));
    }
}
