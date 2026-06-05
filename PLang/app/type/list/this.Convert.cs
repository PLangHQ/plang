namespace app.type.list;

public sealed partial class @this
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
            return global::app.data.@this.Ok(new @this { Context = context });
        return null;
    }
}
