using app.variable;
using number = global::app.type.number.@this;

namespace app.module.math;

[Action("abs")]
public partial class Abs : IContext
{
    public partial data.@this Value { get; init; }

    [Code]
    public partial global::app.module.math.code.IMath Math { get; }

    public async Task<data.@this<number>> Run() => await Math.Abs(this);
}
