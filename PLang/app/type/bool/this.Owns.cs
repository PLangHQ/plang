namespace app.type.@bool;

public sealed partial class @this
{
    /// <summary><c>bool</c> owns the CLR <c>bool</c>. (Distributed <c>OwnerOf</c>.)</summary>
    public static System.Collections.Generic.IReadOnlyList<global::app.type.convert.OwnedClr> OwnedClrTypes { get; }
        = new[] { new global::app.type.convert.OwnedClr(typeof(bool)) };
}
