using app;

namespace app.module.list.type;

public sealed class list : global::app.type.item.@this, global::app.type.item.ICreate<list>
{
    [Out] public int count { get; init; }
    [Out] public object? value { get; init; }

    // This class lives in `app.module.list.type`, so the default namespace-tail
    // derivation would mint "type". The value IS a list — mint "list".
    protected internal override global::app.type.@this Mint() => new("list");
}
