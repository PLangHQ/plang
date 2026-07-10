namespace app.type.item.list;

public partial class @this
{
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
