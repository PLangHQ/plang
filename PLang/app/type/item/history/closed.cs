namespace app.type.item.history;

/// <summary>The history of a value that is never made from another — a type: what reads a name to it finds it, so it
/// takes no prior, whatever reads it, and stays empty.</summary>
internal sealed class closed : @this
{
    public override void Add(global::app.type.item.@this? prior) { }
}
