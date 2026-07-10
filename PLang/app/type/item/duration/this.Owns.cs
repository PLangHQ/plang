namespace app.type.item.duration;

public sealed partial class @this
{
    /// <summary><c>duration</c> owns the CLR <c>TimeSpan</c>. (Distributed <c>OwnerOf</c>.)</summary>
    public static System.Collections.Generic.IReadOnlyList<global::app.type.convert.OwnedClr> OwnedClrTypes { get; }
        = new[] { new global::app.type.convert.OwnedClr(typeof(System.TimeSpan), "duration") };
}
