namespace PLang.Tests.App.Types.Probe;

/// <summary>A collected concept for CollectedTypeTests — an item that answers for its own key.</summary>
[global::app.Attributes.PlangType("probe")]
public sealed class @this : global::app.type.item.@this,
    global::app.type.item.ICreate<@this>, global::app.type.item.IMatch<@this>,
    global::app.type.item.ICurrent<@this>, global::app.type.item.ILoad<@this>,
    global::app.type.item.IList<@this, global::app.type.item.list.@this<@this>>
{
    public static global::app.type.item.list.@this<@this> List(global::app.@this app) => new();

    public string Key { get; }
    public @this(string key) { Key = key; }

    public System.Threading.Tasks.ValueTask<@this?> Match(string key)
        => new(string.Equals(Key, key, System.StringComparison.OrdinalIgnoreCase) ? this : null);
}
