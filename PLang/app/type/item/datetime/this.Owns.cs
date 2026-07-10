namespace app.type.item.datetime;

public sealed partial class @this
{
    /// <summary><c>datetime</c> owns the CLR <c>DateTimeOffset</c> and plain
    /// <c>DateTime</c> (lifted via <see cref="Convert"/>, which offsets an
    /// Unspecified/Local <c>DateTime</c> through <c>new DateTimeOffset(dt)</c>).
    /// (Distributed <c>OwnerOf</c>.)</summary>
    public static System.Collections.Generic.IReadOnlyList<global::app.type.convert.OwnedClr> OwnedClrTypes { get; }
        = new[]
        {
            new global::app.type.convert.OwnedClr(typeof(System.DateTimeOffset), "datetime"),
            new global::app.type.convert.OwnedClr(typeof(System.DateTime), "datetime"),
        };
}
