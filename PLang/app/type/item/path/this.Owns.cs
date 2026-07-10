namespace app.type.item.path;

public abstract partial class @this
{
    /// <summary>
    /// <c>path</c> owns <see cref="@this"/> and every scheme subclass
    /// (file/http/…) — declared <c>Assignable</c> so any path subtype routes to
    /// the path family. (Distributed <c>OwnerOf</c>; replaces the old
    /// <c>path.@this.IsAssignableFrom(u)</c> arm.)
    /// </summary>
    public static System.Collections.Generic.IReadOnlyList<global::app.type.convert.OwnedClr> OwnedClrTypes { get; }
        = new[] { new global::app.type.convert.OwnedClr(typeof(@this), Assignable: true) };
}
