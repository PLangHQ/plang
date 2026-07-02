using app.variable;
using number = global::app.type.number.@this;

namespace app.module.math;

[Action("sqrt")]
public partial class Sqrt : IContext
{
    public partial data.@this Value { get; init; }

    [Code]
    public partial global::app.module.math.code.IMath Math { get; }

    public async Task<data.@this<number>> Run() => await Math.Sqrt(this);
}
