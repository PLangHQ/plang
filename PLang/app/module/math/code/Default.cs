using app.module.code;

namespace app.module.math.code;

using number = global::app.type.number.@this;
using OverflowMode = global::app.type.number.OverflowMode;
using PrecisionMode = global::app.type.number.PrecisionMode;
using OverflowParam = global::app.data.@this<global::app.type.choice.@this<global::app.type.number.OverflowMode>>;
using PrecisionParam = global::app.data.@this<global::app.type.choice.@this<global::app.type.number.PrecisionMode>>;

/// <summary>
/// Default <see cref="IMath"/> — arithmetic on the <c>number</c> value type. The provider has the
/// action's context, so it owns the Data-wrapping: a pure <c>number</c> op returns its value and
/// throws a keyed AppException on error; <see cref="Wrap"/> turns that into a native plang Data
/// error with context. Overflow/precision are settings resolved onto the action's params by the
/// setting cascade (step → %!math.&lt;action&gt;.overflow% → %!math.overflow%); the handler reads
/// them and applies the lenient default (Promote / Error) when unset.
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

    private static data.@this<number> Invalid(actor.context.@this ctx, string op, string need)
        => ctx.Error<number>(new global::app.error.ValidationError($"math.{op} requires {need}", "InvalidInput"));

    // The resolved overflow/precision setting for this step (the param carries step → %!math.*%);
    // the lenient default applies when nothing set it.
    private static async Task<OverflowMode> Overflow(OverflowParam? p) => (await p!.Value())?.Value ?? OverflowMode.Promote;
    private static async Task<PrecisionMode> Precision(PrecisionParam? p) => (await p!.Value())?.Value ?? PrecisionMode.Error;

    public async Task<data.@this<number>> Add(Add action)
    {
        var a = await action.A.Value<number>();
        var b = await action.B.Value<number>();
        if (a == null || b == null) return Invalid(action.Context, "add", "two numbers");
        var overflow = await Overflow(action.Overflow);
        var precision = await Precision(action.Precision);
        return Wrap(action.Context, () => a.Add(b, overflow, precision));
    }

    public async Task<data.@this<number>> Subtract(Subtract action)
    {
        var a = await action.A.Value<number>();
        var b = await action.B.Value<number>();
        if (a == null || b == null) return Invalid(action.Context, "subtract", "two numbers");
        var overflow = await Overflow(action.Overflow);
        var precision = await Precision(action.Precision);
        return Wrap(action.Context, () => a.Subtract(b, overflow, precision));
    }

    public async Task<data.@this<number>> Multiply(Multiply action)
    {
        var a = await action.A.Value<number>();
        var b = await action.B.Value<number>();
        if (a == null || b == null) return Invalid(action.Context, "multiply", "two numbers");
        var overflow = await Overflow(action.Overflow);
        var precision = await Precision(action.Precision);
        return Wrap(action.Context, () => a.Multiply(b, overflow, precision));
    }

    public async Task<data.@this<number>> Divide(Divide action)
    {
        var a = await action.A.Value<number>();
        var b = await action.B.Value<number>();
        if (a == null || b == null) return Invalid(action.Context, "divide", "two numbers");
        var overflow = await Overflow(action.Overflow);
        var precision = await Precision(action.Precision);
        return Wrap(action.Context, () => a.Divide(b, overflow, precision));
    }

    public async Task<data.@this<number>> IntDivide(IntDiv action)
    {
        var a = await action.A.Value<number>();
        var b = await action.B.Value<number>();
        if (a == null || b == null) return Invalid(action.Context, "intdiv", "two numbers");
        var overflow = await Overflow(action.Overflow);
        var precision = await Precision(action.Precision);
        return Wrap(action.Context, () => a.IntDivide(b, overflow, precision));
    }

    public async Task<data.@this<number>> Modulo(Modulo action)
    {
        var a = await action.A.Value<number>();
        var b = await action.B.Value<number>();
        if (a == null || b == null) return Invalid(action.Context, "modulo", "two numbers");
        var overflow = await Overflow(action.Overflow);
        var precision = await Precision(action.Precision);
        return Wrap(action.Context, () => a.Modulo(b, overflow, precision));
    }

    public async Task<data.@this<number>> Power(Power action)
    {
        var a = await action.Base.Value<number>();
        var b = await action.Exponent.Value<number>();
        if (a == null || b == null) return Invalid(action.Context, "power", "base and exponent");
        var overflow = await Overflow(action.Overflow);
        var precision = await Precision(action.Precision);
        return Wrap(action.Context, () => a.Power(b, overflow, precision));
    }

    public async Task<data.@this<number>> Min(Min action)
    {
        var a = await action.A.Value<number>();
        var b = await action.B.Value<number>();
        return a == null || b == null ? Invalid(action.Context, "min", "two numbers")
            : Wrap(action.Context, () => a.Min(b));
    }

    public async Task<data.@this<number>> Max(Max action)
    {
        var a = await action.A.Value<number>();
        var b = await action.B.Value<number>();
        return a == null || b == null ? Invalid(action.Context, "max", "two numbers")
            : Wrap(action.Context, () => a.Max(b));
    }

    public async Task<data.@this<number>> Abs(Abs action)
    {
        var n = await action.Value.Value<number>();
        return n == null ? Invalid(action.Context, "abs", "a number")
            : Wrap(action.Context, () => number.Abs(n));
    }

    public async Task<data.@this<number>> Ceiling(Ceiling action)
    {
        var n = await action.Value.Value<number>();
        return n == null ? Invalid(action.Context, "ceiling", "a number")
            : Wrap(action.Context, () => number.Ceiling(n));
    }

    public async Task<data.@this<number>> Floor(Floor action)
    {
        var n = await action.Value.Value<number>();
        return n == null ? Invalid(action.Context, "floor", "a number")
            : Wrap(action.Context, () => number.Floor(n));
    }

    public async Task<data.@this<number>> Sqrt(Sqrt action)
    {
        var n = await action.Value.Value<number>();
        return n == null ? Invalid(action.Context, "sqrt", "a number")
            : Wrap(action.Context, () => number.Sqrt(n));
    }

    public async Task<data.@this<number>> Round(Round action)
    {
        var n = await action.Value.Value<number>();
        if (n == null) return Invalid(action.Context, "round", "a number");
        var decimals = (await action.Decimals.Value())!;
        return Wrap(action.Context, () => number.Round(n, decimals));
    }
}
