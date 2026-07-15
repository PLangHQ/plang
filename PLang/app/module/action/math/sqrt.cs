using app.variable;
using number = global::app.type.item.number.@this;

namespace app.module.action.math;

[Action("sqrt")]
public partial class Sqrt : IContext
{
    public partial data.@this Value { get; init; }

    [Code]
    public partial global::app.module.action.math.code.IMath Math { get; }

    public async Task<data.@this<number>> Run() => await Math.Sqrt(this);
}
