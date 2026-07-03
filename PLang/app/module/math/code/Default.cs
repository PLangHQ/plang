using app.module.code;

namespace app.module.math.code;

using number = global::app.type.number.@this;
using NumberPolicy = global::app.type.number.NumberPolicy;
using POverflow = global::app.type.number.OverflowMode;
using PPrecision = global::app.type.number.PrecisionMode;
using OverflowChoice = global::app.data.@this<global::app.type.choice.@this<global::app.type.number.OverflowMode>>;
using PrecisionChoice = global::app.data.@this<global::app.type.choice.@this<global::app.type.number.PrecisionMode>>;

/// <summary>
/// Default <see cref="IMath"/> — policy-aware arithmetic on the <c>number</c> value type.
/// The provider has the action's context, so it owns the Data-wrapping: a pure <c>number</c>
/// op returns its value and throws a keyed AppException on error; <see cref="Wrap"/> turns
/// that into a native plang Data error with context.
/// </summary>
public sealed class Default : IMath
{
    public string Name => "default";
    public bool IsDefault { get; set; }
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    private static data.@this<number> Wrap(actor.context.@this ctx, System.Func<number> compute)
    {
        try { return ctx.Ok<number>(compute()); }
        catch (global::app.error.AppException ex)
        {
            return ctx.Error<number>(new global::app.error.Error(ex.Message, ex.Key, ex.StatusCode) { Exception = ex });
        }
    }

    private static async System.Threading.Tasks.Task<NumberPolicy> Policy(
        actor.context.@this ctx, OverflowChoice? overflow, PrecisionChoice? precision)
        => MathPolicy.Resolve(ctx,
            overflow == null ? null : (await overflow.Value())?.Value,
            precision == null ? null : (await precision.Value())?.Value);

    private static data.@this<number> Invalid(actor.context.@this ctx, string op, string need)
        => ctx.Error<number>(new global::app.error.ValidationError($"math.{op} requires {need}", "InvalidInput"));

    public async Task<data.@this<number>> Add(Add action)
    {
        var policy = await Policy(action.Context, action.Overflow, action.Precision);
        var an = number.FromObject(await action.A.Value());
        var bn = number.FromObject(await action.B.Value());
        return an == null || bn == null ? Invalid(action.Context, "add", "two numbers")
            : Wrap(action.Context, () => number.Add(an, bn, policy));
    }

    public async Task<data.@this<number>> Subtract(Subtract action)
    {
        var policy = await Policy(action.Context, action.Overflow, action.Precision);
        var an = number.FromObject(await action.A.Value());
        var bn = number.FromObject(await action.B.Value());
        return an == null || bn == null ? Invalid(action.Context, "subtract", "two numbers")
            : Wrap(action.Context, () => number.Subtract(an, bn, policy));
    }

    public async Task<data.@this<number>> Multiply(Multiply action)
    {
        var policy = await Policy(action.Context, action.Overflow, action.Precision);
        var an = number.FromObject(await action.A.Value());
        var bn = number.FromObject(await action.B.Value());
        return an == null || bn == null ? Invalid(action.Context, "multiply", "two numbers")
            : Wrap(action.Context, () => number.Multiply(an, bn, policy));
    }

    public async Task<data.@this<number>> Divide(Divide action)
    {
        var policy = await Policy(action.Context, action.Overflow, action.Precision);
        var an = number.FromObject(await action.A.Value());
        var bn = number.FromObject(await action.B.Value());
        return an == null || bn == null ? Invalid(action.Context, "divide", "two numbers")
            : Wrap(action.Context, () => number.Divide(an, bn, policy));
    }

    public async Task<data.@this<number>> IntDivide(IntDiv action)
    {
        var policy = await Policy(action.Context, action.Overflow, action.Precision);
        var an = number.FromObject(await action.A.Value());
        var bn = number.FromObject(await action.B.Value());
        return an == null || bn == null ? Invalid(action.Context, "intdiv", "two numbers")
            : Wrap(action.Context, () => number.IntDivide(an, bn, policy));
    }

    public async Task<data.@this<number>> Modulo(Modulo action)
    {
        var policy = await Policy(action.Context, action.Overflow, action.Precision);
        var an = number.FromObject(await action.A.Value());
        var bn = number.FromObject(await action.B.Value());
        return an == null || bn == null ? Invalid(action.Context, "modulo", "two numbers")
            : Wrap(action.Context, () => number.Modulo(an, bn, policy));
    }

    public async Task<data.@this<number>> Power(Power action)
    {
        var policy = await Policy(action.Context, action.Overflow, action.Precision);
        var an = number.FromObject(await action.Base.Value());
        var bn = number.FromObject(await action.Exponent.Value());
        return an == null || bn == null ? Invalid(action.Context, "power", "base and exponent")
            : Wrap(action.Context, () => number.Power(an, bn, policy));
    }

    public async Task<data.@this<number>> Min(Min action)
    {
        var policy = await Policy(action.Context, action.Overflow, action.Precision);
        var an = number.FromObject(await action.A.Value());
        var bn = number.FromObject(await action.B.Value());
        return an == null || bn == null ? Invalid(action.Context, "min", "two numbers")
            : Wrap(action.Context, () => number.Min(an, bn, policy));
    }

    public async Task<data.@this<number>> Max(Max action)
    {
        var policy = await Policy(action.Context, action.Overflow, action.Precision);
        var an = number.FromObject(await action.A.Value());
        var bn = number.FromObject(await action.B.Value());
        return an == null || bn == null ? Invalid(action.Context, "max", "two numbers")
            : Wrap(action.Context, () => number.Max(an, bn, policy));
    }

    public async Task<data.@this<number>> Abs(Abs action)
    {
        var n = number.FromObject(await action.Value.Value());
        return n == null ? Invalid(action.Context, "abs", "a number")
            : Wrap(action.Context, () => number.Abs(n));
    }

    public async Task<data.@this<number>> Ceiling(Ceiling action)
    {
        var n = number.FromObject(await action.Value.Value());
        return n == null ? Invalid(action.Context, "ceiling", "a number")
            : Wrap(action.Context, () => number.Ceiling(n));
    }

    public async Task<data.@this<number>> Floor(Floor action)
    {
        var n = number.FromObject(await action.Value.Value());
        return n == null ? Invalid(action.Context, "floor", "a number")
            : Wrap(action.Context, () => number.Floor(n));
    }

    public async Task<data.@this<number>> Sqrt(Sqrt action)
    {
        var n = number.FromObject(await action.Value.Value());
        return n == null ? Invalid(action.Context, "sqrt", "a number")
            : Wrap(action.Context, () => number.Sqrt(n));
    }

    public async Task<data.@this<number>> Round(Round action)
    {
        var n = number.FromObject(await action.Value.Value());
        if (n == null) return Invalid(action.Context, "round", "a number");
        var decimals = (await action.Decimals.Value())!;
        return Wrap(action.Context, () => number.Round(n, decimals));
    }
}
