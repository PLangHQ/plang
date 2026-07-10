namespace app.type.item.list;

public partial class @this
{
    /// <summary>
    /// OBP: <c>list</c> owns construction from a raw conversion source. A list-typed
    /// slot fed a blank string yields an empty list — <c>set %x% = []</c> serializes
    /// as <c>Value="" Type="list"</c>, and the runtime must build an empty collection
    /// rather than fail the string→list conversion. Populated JSON-array strings and
    /// every other source shape are declined (<c>null</c>): the conversion dispatcher's
    /// JSON-deserialize path + <see cref="Json"/>.Read rebuild those.
    /// </summary>
    public static global::app.data.@this? Convert(object? value, string? kind,
        global::app.actor.context.@this context)
    {
        if (value is string s && string.IsNullOrWhiteSpace(s))
            return context.Ok(new @this(context));
        return null;
    }

    /// <summary>
    /// Build-at-edge: coerce a raw value to a native list, or <c>null</c> when it
    /// isn't list-shaped. An already-native list passes through unchanged (so
    /// in-place ops keep mutating the stored instance); a raw <c>IEnumerable</c>
    /// (non-string) is wrapped element-by-element as Data, with nested raw lists
    /// converted recursively — so every list op runs the single native compare path
    /// and no raw <c>List&lt;object?&gt;</c> survives to diverge.
    /// </summary>
    public static @this? FromRaw(object? value, global::app.actor.context.@this? context)
    {
        if (value is @this native) return native;
        if (value is string || value is not System.Collections.IEnumerable seq) return null;

        var list = new @this(context);
        foreach (var item in seq)
        {
            if (item is global::app.data.@this existing) { list.Add(existing); continue; }
            object? element = item is System.Collections.IEnumerable and not string
                ? FromRaw(item, context) ?? item
                : item;
            list.Add(new global::app.data.@this("", element, context: context));
        }
        return list;
    }
}
