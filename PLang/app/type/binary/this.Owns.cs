namespace app.type.binary;

public sealed partial class @this
{
    /// <summary><c>binary</c> owns the CLR <c>byte[]</c>. (Distributed <c>OwnerOf</c>.)</summary>
    public static System.Collections.Generic.IReadOnlyList<global::app.type.convert.OwnedClr> OwnedClrTypes { get; }
        = new[] { new global::app.type.convert.OwnedClr(typeof(byte[]), "binary") };
}
