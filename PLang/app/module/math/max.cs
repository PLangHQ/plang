using app.variable;
using number = global::app.type.number.@this;

namespace app.module.math;

[Action("max")]
public partial class Max : IContext
{
    public partial data.@this A { get; init; }
    public partial data.@this B { get; init; }


    [Code]
    public partial global::app.module.math.code.IMath Math { get; }

    public async Task<data.@this<number>> Run() => await Math.Max(this);
}
