using app.variable;
using number = global::app.type.number.@this;
using POverflow = global::app.type.number.OverflowMode;
using PPrecision = global::app.type.number.PrecisionMode;

namespace app.module.math;

[Action("max")]
public partial class Max : IContext
{
    public partial data.@this A { get; init; }
    public partial data.@this B { get; init; }

    public partial data.@this<global::app.type.choice.@this<POverflow>>? Overflow { get; init; }
    public partial data.@this<global::app.type.choice.@this<PPrecision>>? Precision { get; init; }

    [Code]
    public partial global::app.module.math.code.IMath Math { get; }

    public async Task<data.@this<number>> Run() => await Math.Max(this);
}
