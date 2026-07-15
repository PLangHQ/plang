using app.variable;
using number = global::app.type.item.number.@this;

namespace app.module.action.math;

[Action("max")]
public partial class Max : IContext
{
    public partial data.@this A { get; init; }
    public partial data.@this B { get; init; }


    [Code]
    public partial global::app.module.action.math.code.IMath Math { get; }

    public async Task<data.@this<number>> Run() => await Math.Max(this);
}
