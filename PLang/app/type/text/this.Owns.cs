namespace app.type.text;

public sealed partial class @this
{
    /// <summary><c>text</c> owns the CLR <c>string</c>. (Distributed <c>OwnerOf</c>.)</summary>
    public static System.Collections.Generic.IReadOnlyList<global::app.type.convert.OwnedClr> OwnedClrTypes { get; }
        = new[] { new global::app.type.convert.OwnedClr(typeof(string)) };
}
