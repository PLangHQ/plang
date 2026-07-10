namespace app.type.item.time;

public sealed partial class @this
{
    /// <summary><c>time</c> owns the CLR <c>TimeOnly</c>. (Distributed <c>OwnerOf</c>.)</summary>
    public static System.Collections.Generic.IReadOnlyList<global::app.type.convert.OwnedClr> OwnedClrTypes { get; }
        = new[] { new global::app.type.convert.OwnedClr(typeof(System.TimeOnly), "time") };
}
