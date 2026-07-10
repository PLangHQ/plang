namespace app.type.item.date;

public sealed partial class @this
{
    /// <summary><c>date</c> owns the CLR <c>DateOnly</c>. (Distributed <c>OwnerOf</c>.)</summary>
    public static System.Collections.Generic.IReadOnlyList<global::app.type.convert.OwnedClr> OwnedClrTypes { get; }
        = new[] { new global::app.type.convert.OwnedClr(typeof(System.DateOnly), "date") };
}
