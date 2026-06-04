namespace app.type.number;

public sealed partial class @this
{
    /// <summary>
    /// The CLR numeric types <c>number</c> owns — the full Way-3 scalar tower,
    /// each with its precision kind. The distributed replacement for the central
    /// <c>OwnerOf</c> ladder: adding a numeric kind is an edit to this list alone.
    /// </summary>
    public static System.Collections.Generic.IReadOnlyList<global::app.type.convert.OwnedClr> OwnedClrTypes { get; }
        = new[]
        {
            new global::app.type.convert.OwnedClr(typeof(sbyte), "sbyte"),
            new global::app.type.convert.OwnedClr(typeof(byte), "byte"),
            new global::app.type.convert.OwnedClr(typeof(short), "short"),
            new global::app.type.convert.OwnedClr(typeof(ushort), "ushort"),
            new global::app.type.convert.OwnedClr(typeof(int), "int"),
            new global::app.type.convert.OwnedClr(typeof(uint), "uint"),
            new global::app.type.convert.OwnedClr(typeof(long), "long"),
            new global::app.type.convert.OwnedClr(typeof(ulong), "ulong"),
            new global::app.type.convert.OwnedClr(typeof(System.Int128), "int128"),
            new global::app.type.convert.OwnedClr(typeof(System.UInt128), "uint128"),
            new global::app.type.convert.OwnedClr(typeof(System.Numerics.BigInteger), "biginteger"),
            new global::app.type.convert.OwnedClr(typeof(System.Half), "half"),
            new global::app.type.convert.OwnedClr(typeof(float), "float"),
            new global::app.type.convert.OwnedClr(typeof(double), "double"),
            new global::app.type.convert.OwnedClr(typeof(decimal), "decimal"),
        };
}
