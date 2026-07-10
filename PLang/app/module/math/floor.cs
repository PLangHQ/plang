using app.variable;
using number = global::app.type.item.number.@this;

namespace app.module.math;

[Action("floor")]
public partial class Floor : IContext
{
    public partial data.@this Value { get; init; }

    [Code]
    public partial global::app.module.math.code.IMath Math { get; }

    public async Task<data.@this<number>> Run() => await Math.Floor(this);
}
