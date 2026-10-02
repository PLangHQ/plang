namespace app.type.item.text;

public sealed partial class @this
{
    /// <summary>This text in the pieces <paramref name="separator"/> cuts it into — the empty ones kept or dropped as
    /// <paramref name="empty"/> says. A separator or a choice that didn't resolve is its own answer. A new list, born
    /// through its type.</summary>
    public global::System.Threading.Tasks.Task<global::app.data.@this> Split(global::app.data.@this<@this> separator,
        global::app.data.@this<global::app.type.item.choice.@this<global::app.type.item.text.empty>> empty,
        global::app.actor.context.@this context)
        => separator.Use(cut => empty.Use(async pieces =>
        {
            var cutUp = _value.Split(cut._value, pieces.Value == global::app.type.item.text.empty.keep
                ? global::System.StringSplitOptions.None : global::System.StringSplitOptions.RemoveEmptyEntries);
            return await context.App.type.list["list"].Create(cutUp, context);
        }));
}
