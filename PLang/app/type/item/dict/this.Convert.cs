namespace app.type.item.dict;

public sealed partial class @this
{
    /// <summary>Build a native dict from a raw CLR dictionary — each entry wrapped as a Data row.</summary>
    public static @this FromRaw(System.Collections.IDictionary raw, global::app.actor.context.@this? context)
    {
        var d = new @this(context);
        foreach (System.Collections.DictionaryEntry e in raw)
            d.Set(e.Key.ToString()!, e.Value);
        return d;
    }

    /// <summary>
    /// OBP: <c>dict</c> owns construction from a raw conversion source. A dict-typed
    /// slot fed a blank string yields an empty dict — <c>set %x% = {}</c> serializes
    /// as <c>Value="" Type="dict"</c>, and the runtime must build an empty collection
    /// rather than fail the string→dict conversion. Populated JSON-object strings and
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
}
