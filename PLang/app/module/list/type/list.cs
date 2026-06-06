using app;

namespace app.module.list.type;

public sealed class list : global::app.type.item.@this
{
    [Out] public int count { get; init; }
    [Out] public object? value { get; init; }
}
