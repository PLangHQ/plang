namespace app.type.guid;

public sealed partial class @this
{
    /// <summary><c>guid</c> owns the CLR <c>System.Guid</c>. (Distributed <c>OwnerOf</c>.)</summary>
    public static System.Collections.Generic.IReadOnlyList<global::app.type.convert.OwnedClr> OwnedClrTypes { get; }
        = new[] { new global::app.type.convert.OwnedClr(typeof(System.Guid), "guid") };
}
