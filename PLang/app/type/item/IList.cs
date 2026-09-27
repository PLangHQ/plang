namespace app.type.item;

/// <summary>
/// Which list holds the items of its kind — the list its type answers as <c>list</c>. A plain
/// <c>list&lt;T&gt;</c> by default; a concept with its own work (goal's loading, the types' lookups
/// by other keys) answers its own list. Shares its short name with .NET's <c>IList&lt;T&gt;</c>:
/// outside <c>app.type.item</c> it is written <c>item.IList&lt;…&gt;</c>.
/// </summary>
public interface IList<TSelf> where TSelf : @this, ICreate<TSelf>, IList<TSelf>
{
    static virtual list.@this<TSelf> List(global::app.@this app) => new();
}
