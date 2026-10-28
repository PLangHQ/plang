namespace app.snapshot;

/// <summary>
/// Snapshot — navigation. A snapshot read back is what the app wrote, so navigating and editing it is that dict's own:
/// <c>%snap.app.actor.list.user.context.variable.list.x%</c> reads the user's saved <c>x</c>, and
/// <c>set %snap.app.….x% = 2</c> edits it. The snapshot knows nothing of what each object keeps; each reads its own back
/// on a resume.
/// </summary>
public sealed partial class @this
{
    /// <summary>A child read — what the app wrote, by key: opened once, the first time it is navigated, and kept open, so
    /// an edit made through it is the snapshot's.</summary>
    public override System.Threading.Tasks.ValueTask<data.@this> Get(data.@this parent, string key)
    {
        if (_written is not { } written) return new(parent.Context.NotFound(key));
        if (written.Get(key, parent.Context) is { } held && held.Peek() is global::app.type.item.source
            && View.Make(parent.Context.App.type.list[typeof(global::app.type.item.dict.@this)], held.Peek(), parent.Context) is { } opened)
            written.Set(new global::app.data.@this(held.Name, opened, context: parent.Context));
        return written.Get(parent, key);
    }

    /// <summary>A child write — sets an entry of what the app wrote (create or overwrite).</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Set(string key, bool isIndex, object? value, global::app.actor.context.@this context)
    {
        if (_written is not { } written) return context.Error(new global::app.error.@this(
            "a snapshot being taken is written as it is: edit one read back", "SnapshotTaken", 400));
        var set = await written.Set(key, isIndex, value, context);
        return set.Success ? context.Ok(this) : set;
    }
}
