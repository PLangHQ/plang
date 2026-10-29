namespace PLang.Tests.Shared;

/// <summary>
/// Reads what an object wrote in a snapshot, in tests: an entry through the value's own typed ask, converted at the use,
/// so an assertion stays one line. Nothing here lowers to CLR beyond the conversion the assertion itself needs.
/// </summary>
public static class SnapshotReadExtensions
{
    /// <summary>A text entry as a string. Null when the entry is absent.</summary>
    public static async Task<string?> Text(this global::app.type.item.dict.@this node, string key, global::app.actor.context.@this context)
        => node.Get(key, context) is { } d
            ? (await d.Value<global::app.type.item.text.@this>()).ToString()
            : null;

    /// <summary>A list entry as its rows. Empty when the entry is absent.</summary>
    public static async Task<IReadOnlyList<global::app.data.@this>> Rows(this global::app.type.item.dict.@this node, string key,
        global::app.actor.context.@this context)
        => node.Get(key, context) is { } d
            ? (await d.Value<global::app.type.item.list.@this>())!.Items(context).ToList()
            : Array.Empty<global::app.data.@this>();
}
