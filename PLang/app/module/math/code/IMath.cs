using app.module.code;

namespace app.module.math.code;

using number = global::app.type.item.number.@this;

/// <summary>
/// Provider that owns the math operations. Default: <see cref="Default"/> (policy-aware
/// arithmetic on the <c>number</c> value type). Swappable like every other <c>[Code]</c>
/// provider (signing, crypto, http). The provider has the action's context, so it owns the
/// error-wrapping — a pure <c>number</c> op throws on failure and the provider turns that
/// into a native plang Data error with context.
/// </summary>
public interface IMath : ICode
{
    Task<data.@this<number>> Add(Add action);
    Task<data.@this<number>> Subtract(Subtract action);
    Task<data.@this<number>> Multiply(Multiply action);
    Task<data.@this<number>> Divide(Divide action);
    Task<data.@this<number>> IntDivide(IntDiv action);
    Task<data.@this<number>> Modulo(Modulo action);
    Task<data.@this<number>> Power(Power action);
    Task<data.@this<number>> Abs(Abs action);
    Task<data.@this<number>> Ceiling(Ceiling action);
    Task<data.@this<number>> Floor(Floor action);
    Task<data.@this<number>> Sqrt(Sqrt action);
    Task<data.@this<number>> Round(Round action);
    Task<data.@this<number>> Min(Min action);
    Task<data.@this<number>> Max(Max action);
}
