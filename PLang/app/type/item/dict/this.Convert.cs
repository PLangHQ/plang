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
}
