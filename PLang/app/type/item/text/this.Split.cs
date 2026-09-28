namespace app.type.item.text;

public sealed partial class @this
{
    /// <summary>This text in the pieces <paramref name="separator"/> cuts it into — leaving out the empty
    /// ones when <paramref name="removeEmpty"/> holds. A separator or a flag that didn't resolve is its own
    /// answer. A new list, born through its type.</summary>
    public global::System.Threading.Tasks.Task<global::app.data.@this> Split(global::app.data.@this<@this> separator,
        global::app.data.@this<global::app.type.item.@bool.@this> removeEmpty, global::app.actor.context.@this context)
        => separator.Use(cut => removeEmpty.Use(async empty =>
        {
            var pieces = _value.Split(cut._value,
                empty.Value ? global::System.StringSplitOptions.RemoveEmptyEntries : global::System.StringSplitOptions.None);
            return await context.App.type.list["list"].Create(pieces, context);
        }));
}
