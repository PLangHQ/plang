using app.variable;

using number = global::app.type.item.number.@this;

namespace app.module.math;

[Action("modulo")]
public partial class Modulo : IContext
{
    public partial data.@this A { get; init; }
    public partial data.@this B { get; init; }
    /// <summary>Integer-overflow mode. Default: Promote (widen; never wrap).</summary>
    [Default(number.Overflow.Promote)]
    public partial data.@this<global::app.type.item.choice.@this<number.Overflow>> Overflow { get; init; }

    /// <summary>Precision mode for a double⊕decimal mix. Default: Error (require an explicit choice).</summary>
    [Default(number.Precision.Error)]
    public partial data.@this<global::app.type.item.choice.@this<number.Precision>> Precision { get; init; }

    [Code]
    public partial global::app.module.math.code.IMath Math { get; }

    public async Task<data.@this<number>> Run() => await Math.Modulo(this);
}
