namespace app.type.number;

/// <summary>
/// Operator surface — <c>+ - * / %</c>, <c>== !=</c>. Delegates to the Way-3
/// arithmetic engine under the default <see cref="NumberPolicy.Lenient"/>
/// (Promote integers, Error on double⊕decimal). Divide leaves the integer
/// track (<c>7 / 2 → 3.5</c>); truncating division is the explicit
/// <c>math.intdiv</c>. A double⊕decimal mix throws (no precision choice on the
/// raw operator path) — the named <c>math.*</c> methods carry policy.
/// </summary>
public sealed partial class @this
{
    public static @this operator +(@this a, @this b) => DoOp(a, b, ArithOp.Add, NumberPolicy.Lenient);
    public static @this operator -(@this a, @this b) => DoOp(a, b, ArithOp.Sub, NumberPolicy.Lenient);
    public static @this operator *(@this a, @this b) => DoOp(a, b, ArithOp.Mul, NumberPolicy.Lenient);
    public static @this operator /(@this a, @this b) => DoDivide(a, b, NumberPolicy.Lenient);
    public static @this operator %(@this a, @this b) => DoOp(a, b, ArithOp.Mod, NumberPolicy.Lenient);

    public static bool operator ==(@this? a, @this? b)
    {
        if (a is null) return b is null;
        return a.Equals(b);
    }

    public static bool operator !=(@this? a, @this? b) => !(a == b);
}
