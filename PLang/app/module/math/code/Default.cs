using app.module.code;

namespace app.module.math.code;

using number = global::app.type.number.@this;

/// <summary>
/// Default <see cref="IMath"/> — arithmetic on the <c>number</c> value type. Each op reads its
/// operands and its overflow/precision (whole plang <c>choice</c> values, already resolved onto
/// the action's params) and runs the op on the number carrier. The pure number op throws a keyed
/// error on overflow / divide-by-zero; <c>context.Data</c> borns that as a native plang Data error.
/// </summary>
public sealed class Default : IMath
{
    public string Name => "default";
    public bool IsDefault { get; set; }
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    private static data.@this<number> Invalid(actor.context.@this context, string op, string need)
        => context.Error<number>(new global::app.error.ValidationError($"math.{op} requires {need}", "InvalidInput"));

    public async Task<data.@this<number>> Add(Add action)
    {
        var a = await action.A.Value<number>();
        var b = await action.B.Value<number>();
        if (a == null || b == null) return Invalid(action.Context, "add", "two numbers");
        var overflow = (await action.Overflow.Value())!;
        var precision = (await action.Precision.Value())!;
        return action.Context.Data(() => a.Add(b, overflow, precision));
    }

    public async Task<data.@this<number>> Subtract(Subtract action)
    {
        var a = await action.A.Value<number>();
        var b = await action.B.Value<number>();
        if (a == null || b == null) return Invalid(action.Context, "subtract", "two numbers");
        var overflow = (await action.Overflow.Value())!;
        var precision = (await action.Precision.Value())!;
        return action.Context.Data(() => a.Subtract(b, overflow, precision));
    }

    public async Task<data.@this<number>> Multiply(Multiply action)
    {
        var a = await action.A.Value<number>();
        var b = await action.B.Value<number>();
        if (a == null || b == null) return Invalid(action.Context, "multiply", "two numbers");
        var overflow = (await action.Overflow.Value())!;
        var precision = (await action.Precision.Value())!;
        return action.Context.Data(() => a.Multiply(b, overflow, precision));
    }

    public async Task<data.@this<number>> Divide(Divide action)
    {
        var a = await action.A.Value<number>();
        var b = await action.B.Value<number>();
        if (a == null || b == null) return Invalid(action.Context, "divide", "two numbers");
        var overflow = (await action.Overflow.Value())!;
        var precision = (await action.Precision.Value())!;
        return action.Context.Data(() => a.Divide(b, overflow, precision));
    }

    public async Task<data.@this<number>> IntDivide(IntDiv action)
    {
        var a = await action.A.Value<number>();
        var b = await action.B.Value<number>();
        if (a == null || b == null) return Invalid(action.Context, "intdiv", "two numbers");
        var overflow = (await action.Overflow.Value())!;
        var precision = (await action.Precision.Value())!;
        return action.Context.Data(() => a.IntDivide(b, overflow, precision));
    }

    public async Task<data.@this<number>> Modulo(Modulo action)
    {
        var a = await action.A.Value<number>();
        var b = await action.B.Value<number>();
        if (a == null || b == null) return Invalid(action.Context, "modulo", "two numbers");
        var overflow = (await action.Overflow.Value())!;
        var precision = (await action.Precision.Value())!;
        return action.Context.Data(() => a.Modulo(b, overflow, precision));
    }

    public async Task<data.@this<number>> Power(Power action)
    {
        var a = await action.Base.Value<number>();
        var b = await action.Exponent.Value<number>();
        if (a == null || b == null) return Invalid(action.Context, "power", "base and exponent");
        var overflow = (await action.Overflow.Value())!;
        var precision = (await action.Precision.Value())!;
        return action.Context.Data(() => a.Power(b, overflow, precision));
    }

    public async Task<data.@this<number>> Min(Min action)
    {
        var a = await action.A.Value<number>();
        var b = await action.B.Value<number>();
        return a == null || b == null ? Invalid(action.Context, "min", "two numbers")
            : action.Context.Data(() => a.Min(b));
    }

    public async Task<data.@this<number>> Max(Max action)
    {
        var a = await action.A.Value<number>();
        var b = await action.B.Value<number>();
        return a == null || b == null ? Invalid(action.Context, "max", "two numbers")
            : action.Context.Data(() => a.Max(b));
    }

    public async Task<data.@this<number>> Abs(Abs action)
    {
        var n = await action.Value.Value<number>();
        return n == null ? Invalid(action.Context, "abs", "a number")
            : action.Context.Data(() => number.Abs(n));
    }

    public async Task<data.@this<number>> Ceiling(Ceiling action)
    {
        var n = await action.Value.Value<number>();
        return n == null ? Invalid(action.Context, "ceiling", "a number")
            : action.Context.Data(() => number.Ceiling(n));
    }

    public async Task<data.@this<number>> Floor(Floor action)
    {
        var n = await action.Value.Value<number>();
        return n == null ? Invalid(action.Context, "floor", "a number")
            : action.Context.Data(() => number.Floor(n));
    }

    public async Task<data.@this<number>> Sqrt(Sqrt action)
    {
        var n = await action.Value.Value<number>();
        return n == null ? Invalid(action.Context, "sqrt", "a number")
            : action.Context.Data(() => number.Sqrt(n));
    }

    public async Task<data.@this<number>> Round(Round action)
    {
        var n = await action.Value.Value<number>();
        if (n == null) return Invalid(action.Context, "round", "a number");
        var decimals = (await action.Decimals.Value())!;
        return action.Context.Data(() => number.Round(n, decimals));
    }
}
