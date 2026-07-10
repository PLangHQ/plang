namespace app.type.item.number;

/// <summary>
/// Operator surface — <c>+ - * / %</c>, <c>== !=</c>. Delegates to the Way-3
/// arithmetic engine under the lenient defaults (Promote integers, Error on
/// double⊕decimal). Divide leaves the integer track (<c>7 / 2 → 3.5</c>);
/// truncating division is the explicit <c>math.intdiv</c>. A double⊕decimal mix
/// throws (no precision choice on the raw operator path) — the named <c>math.*</c>
/// ops carry the overflow/precision settings.
/// </summary>
public sealed partial class @this
{
    public static @this operator +(@this a, @this b) => DoOp(a, b, ArithOp.Add, Overflow.Promote, Precision.Error);
    public static @this operator -(@this a, @this b) => DoOp(a, b, ArithOp.Sub, Overflow.Promote, Precision.Error);
    public static @this operator *(@this a, @this b) => DoOp(a, b, ArithOp.Mul, Overflow.Promote, Precision.Error);
    public static @this operator /(@this a, @this b) => DoDivide(a, b, Precision.Error);
    public static @this operator %(@this a, @this b) => DoOp(a, b, ArithOp.Mod, Overflow.Promote, Precision.Error);

    public static bool operator ==(@this? a, @this? b)
    {
        if (a is null) return b is null;
        return a.Equals(b);
    }

    public static bool operator !=(@this? a, @this? b) => !(a == b);

    // Ordering — rides the numeric-tower CompareTo (IComparable<@this>); the
    // implicit lifts from int/long/... make `count < limit` read naturally at
    // C# call sites that hold a typed number.
    public static bool operator <(@this a, @this b) => a.CompareTo(b) < 0;
    public static bool operator >(@this a, @this b) => a.CompareTo(b) > 0;
    public static bool operator <=(@this a, @this b) => a.CompareTo(b) <= 0;
    public static bool operator >=(@this a, @this b) => a.CompareTo(b) >= 0;
}
