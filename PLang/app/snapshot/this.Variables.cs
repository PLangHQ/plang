namespace app.snapshot;

/// <summary>
/// Snapshot — navigation. A snapshot is a plain container: its entries are a dict of Data, and a
/// section is an entry whose value is a snapshot. So navigating and editing it is the dict's own
/// navigation over its entries — <c>%snap.variables.x%</c> reads section "variables", entry "x", and
/// <c>set %snap.variables.x% = 2</c> edits that entry. The snapshot knows nothing of what an owner
/// keeps in its section; the owner reads its section back on restore.
/// </summary>
public sealed partial class @this
{
    /// <summary>A child read — one of this snapshot's entries (a section, or an entry of one).</summary>
    public override System.Threading.Tasks.ValueTask<data.@this> Get(data.@this parent, string key)
        => Entries.Get(parent, key);

    /// <summary>A child write — sets one of this snapshot's entries (create or overwrite).</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Set(string key, bool isIndex, object? value, global::app.actor.context.@this context)
    {
        await Entries.Set(key, isIndex, value, context);
        return this;
    }
}
