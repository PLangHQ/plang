namespace app.type.item;

/// <summary>
/// Which list holds the items of its kind — the list its type answers as <c>list</c>, and that
/// list's class <typeparamref name="L"/>: a plain <c>list&lt;T&gt;</c> for a concept with nothing
/// special, its own list for a concept with its own work (goal's loading, the types' lookups by
/// other keys). Shares its short name with .NET's <c>IList&lt;T&gt;</c>: outside <c>app.type.item</c>
/// it is written <c>item.IList&lt;…&gt;</c>.
/// </summary>
public interface IList<TSelf, L>
    where TSelf : @this, ICreate<TSelf>, IList<TSelf, L>
    where L : list.@this<TSelf>
{
    static abstract L List(global::app.@this app);
}
