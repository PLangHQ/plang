using app.variable;
using number = global::app.type.item.number.@this;

namespace app.module.action.math;

[Action("round")]
public partial class Round : IContext
{
    public partial data.@this Value { get; init; }
    [Default(0)]
    public partial data.@this<global::app.type.item.number.@this> Decimals { get; init; }

    [Code]
    public partial global::app.module.action.math.code.IMath Math { get; }

    public async Task<data.@this<number>> Run() => await Math.Round(this);
}
