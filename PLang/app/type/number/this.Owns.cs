namespace app.type.number;

public sealed partial class @this
{
    /// <summary>
    /// The CLR numeric types <c>number</c> owns, each with its precision kind.
    /// The distributed replacement for the central <c>OwnerOf</c> ladder — Stage 2
    /// extends this list (uint/ulong/Int128/BigInteger/…) by editing only here.
    /// </summary>
    public static System.Collections.Generic.IReadOnlyList<global::app.type.convert.OwnedClr> OwnedClrTypes { get; }
        = new[]
        {
            new global::app.type.convert.OwnedClr(typeof(int), "int"),
            new global::app.type.convert.OwnedClr(typeof(long), "long"),
            new global::app.type.convert.OwnedClr(typeof(decimal), "decimal"),
            new global::app.type.convert.OwnedClr(typeof(double), "double"),
            new global::app.type.convert.OwnedClr(typeof(float), "float"),
        };
}
