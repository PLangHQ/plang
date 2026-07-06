namespace app.type.number;

// The overflow / precision axes for arithmetic. Nested in the number value so they read as
// number.Overflow / number.Precision. Both are settings resolved onto each math action's params
// by the setting cascade; the ops take them as whole values (a.Add(b, overflow, precision)).
public sealed partial class @this
{
    /// <summary>
    /// Integer overflow axis.
    /// <c>Promote</c> (default): compute on a <c>BigInteger</c> carrier, narrow the result to the
    /// smallest kind that holds it — <c>int+int</c> overflow → <c>long</c>. Never wraps.
    /// <c>Throw</c>: strict-width — keep the operand kind; error if the result doesn't fit.
    /// </summary>
    public enum Overflow { Promote, Throw }

    /// <summary>
    /// Precision-mix axis (<c>double ⊕ decimal</c>). Neither type holds the other exactly, so PLang
    /// refuses to pick silently. <c>Error</c> (default): the developer must choose. <c>Double</c>:
    /// promote to double (IEEE wins). <c>Decimal</c>: promote to decimal (throws if the double is
    /// NaN/Infinity/out of range).
    /// </summary>
    public enum Precision { Error, Double, Decimal }
}
