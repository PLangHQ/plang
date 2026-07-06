namespace PLang.Tests;

using number = global::app.type.number.@this;
using OverflowMode = global::app.type.number.OverflowMode;
using PrecisionMode = global::app.type.number.PrecisionMode;

/// <summary>
/// Test-only static shape for the arithmetic-engine unit tests. Production ops are instance
/// (<c>a.Add(b, overflow, precision)</c>); overflow/precision are settings resolved onto the
/// action's params by the setting cascade, so there is no <c>NumberPolicy</c> bundle. These
/// re-expose the old static form + Lenient/Strict pairs so the Way-3 narrowing/overflow tests
/// read unchanged (SC3 — a test-only helper for a shape with no production callers).
/// </summary>
public static class NumberOps
{
    public static readonly (OverflowMode o, PrecisionMode p) Lenient = (OverflowMode.Promote, PrecisionMode.Error);
    public static readonly (OverflowMode o, PrecisionMode p) Strict  = (OverflowMode.Throw,   PrecisionMode.Error);

    public static number Add(number a, number b, (OverflowMode o, PrecisionMode p) x) => a.Add(b, x.o, x.p);
    public static number Subtract(number a, number b, (OverflowMode o, PrecisionMode p) x) => a.Subtract(b, x.o, x.p);
    public static number Multiply(number a, number b, (OverflowMode o, PrecisionMode p) x) => a.Multiply(b, x.o, x.p);
    public static number Divide(number a, number b, (OverflowMode o, PrecisionMode p) x) => a.Divide(b, x.o, x.p);
    public static number IntDivide(number a, number b, (OverflowMode o, PrecisionMode p) x) => a.IntDivide(b, x.o, x.p);
    public static number Modulo(number a, number b, (OverflowMode o, PrecisionMode p) x) => a.Modulo(b, x.o, x.p);
    public static number Power(number a, number b, (OverflowMode o, PrecisionMode p) x) => a.Power(b, x.o, x.p);
    public static number Min(number a, number b) => a.Min(b);
    public static number Max(number a, number b) => a.Max(b);
    public static number Min(number a, number b, (OverflowMode o, PrecisionMode p) _) => a.Min(b);
    public static number Max(number a, number b, (OverflowMode o, PrecisionMode p) _) => a.Max(b);
}
