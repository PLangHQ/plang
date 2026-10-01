namespace app.type.item.text;

public sealed partial class @this
{
    /// <summary>This text in the pieces <paramref name="separator"/> cuts it into — the empty ones kept when
    /// <paramref name="empty"/> holds, else left out. A separator or a flag that didn't resolve is its own
    /// answer. A new list, born through its type.</summary>
    public global::System.Threading.Tasks.Task<global::app.data.@this> Split(global::app.data.@this<@this> separator,
        global::app.data.@this<global::app.type.item.@bool.@this> empty, global::app.actor.context.@this context)
        => separator.Use(cut => empty.Use(async kept =>
        {
            var pieces = _value.Split(cut._value,
                kept.Value ? global::System.StringSplitOptions.None : global::System.StringSplitOptions.RemoveEmptyEntries);
            return await context.App.type.list["list"].Create(pieces, context);
        }));
}
