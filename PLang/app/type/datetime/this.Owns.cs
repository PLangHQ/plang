namespace app.type.datetime;

public sealed partial class @this
{
    /// <summary><c>datetime</c> owns the CLR <c>DateTimeOffset</c>. (Distributed <c>OwnerOf</c>.)</summary>
    public static System.Collections.Generic.IReadOnlyList<global::app.type.convert.OwnedClr> OwnedClrTypes { get; }
        = new[] { new global::app.type.convert.OwnedClr(typeof(System.DateTimeOffset)) };
}
