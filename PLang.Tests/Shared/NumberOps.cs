namespace PLang.Tests;

using number = global::app.type.number.@this;
using OChoice = global::app.type.choice.@this<global::app.type.number.@this.Overflow>;
using PChoice = global::app.type.choice.@this<global::app.type.number.@this.Precision>;

/// <summary>
/// Test-only static shape for the arithmetic-engine unit tests. Production ops are instance
/// (<c>a.Add(b, overflow, precision)</c>) and take the overflow/precision as whole <c>choice</c>
/// values; overflow/precision are settings resolved onto the action's params by the setting
/// cascade, so there is no <c>NumberPolicy</c> bundle. This re-exposes the old static form +
/// Lenient/Strict so the Way-3 narrowing/overflow tests read unchanged (SC3 — test-only helper).
/// </summary>
public static class NumberOps
{
    public static readonly (number.Overflow o, number.Precision p) Lenient = (number.Overflow.Promote, number.Precision.Error);
    public static readonly (number.Overflow o, number.Precision p) Strict  = (number.Overflow.Throw,   number.Precision.Error);

    public static number Add(number a, number b, (number.Overflow o, number.Precision p) x) => a.Add(b, new OChoice(x.o), new PChoice(x.p));
    public static number Subtract(number a, number b, (number.Overflow o, number.Precision p) x) => a.Subtract(b, new OChoice(x.o), new PChoice(x.p));
    public static number Multiply(number a, number b, (number.Overflow o, number.Precision p) x) => a.Multiply(b, new OChoice(x.o), new PChoice(x.p));
    public static number Divide(number a, number b, (number.Overflow o, number.Precision p) x) => a.Divide(b, new OChoice(x.o), new PChoice(x.p));
    public static number IntDivide(number a, number b, (number.Overflow o, number.Precision p) x) => a.IntDivide(b, new OChoice(x.o), new PChoice(x.p));
    public static number Modulo(number a, number b, (number.Overflow o, number.Precision p) x) => a.Modulo(b, new OChoice(x.o), new PChoice(x.p));
    public static number Power(number a, number b, (number.Overflow o, number.Precision p) x) => a.Power(b, new OChoice(x.o), new PChoice(x.p));
    public static number Min(number a, number b) => a.Min(b);
    public static number Max(number a, number b) => a.Max(b);
    public static number Min(number a, number b, (number.Overflow o, number.Precision p) _) => a.Min(b);
    public static number Max(number a, number b, (number.Overflow o, number.Precision p) _) => a.Max(b);
}
