namespace app.type.item.image;

public sealed partial class @this
{
    /// <summary>
    /// <c>image</c> owns its own wrapper type — a value already shaped as an
    /// <see cref="@this"/> routes to the image family. (Distributed <c>OwnerOf</c>;
    /// matches the old self-owning <c>Discover</c> arm.)
    ///
    /// <para>Note: <c>byte[]</c> is deliberately <em>not</em> declared here.
    /// <c>OwnerOf</c> keys on the conversion <em>target</em>; claiming <c>byte[]</c>
    /// would hijack every <c>byte[]</c>-target conversion into image construction.
    /// An image's raw bytes are decoded by <c>image.Read</c> (the reader side),
    /// not by routing the <c>byte[]</c> CLR target to image.</para>
    /// </summary>
    public static System.Collections.Generic.IReadOnlyList<global::app.type.convert.OwnedClr> OwnedClrTypes { get; }
        = new[] { new global::app.type.convert.OwnedClr(typeof(@this)) };
}
