using app;

namespace app.module.list.type;

public sealed class list : global::app.type.item.@this, global::app.type.item.ICreate<list>
{
    [Out] public int count { get; init; }
    [Out] public object? value { get; init; }
}
