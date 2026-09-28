using number = global::app.type.item.number.@this;

namespace app.module.math;

[Action("min")]
public partial class Min : IContext
{
    public partial data.@this A { get; init; }
    public partial data.@this B { get; init; }


    [Code]
    public partial global::app.module.math.code.IMath Math { get; }

    public async Task<data.@this<number>> Start() => await Math.Min(this);
}
