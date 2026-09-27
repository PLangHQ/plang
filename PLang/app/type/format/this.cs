namespace app.type.format;

/// <summary>
/// The formats a type reads — <c>%!app.type.image.format.list%</c> is image's jpg, png, … kinds, each with its
/// MIMEs and extensions. A format is a kind with a MIME or an extension, so this is a view of the type's own
/// kinds, not a store of its own: adding a format is adding such a kind (<c>app.type.list.Add(kind)</c>, or a
/// DLL's kind class). A type with kinds but no formats (number, path, hash) has an empty list.
/// </summary>
public sealed class @this
{
    private readonly global::app.type.kind.@this _kind;

    /// <param name="kind">The type's own kind — its empty kind holds its kinds; a kinded type has no formats of its own.</param>
    public @this(global::app.type.kind.@this kind) => _kind = kind;

    /// <summary>The type's formats: its own first (plain text is text's, with no name), then its kinds that
    /// are formats.</summary>
    public System.Collections.Generic.IReadOnlyList<global::app.type.kind.@this> list
        => _kind is global::app.type.kind.empty.@this root
            ? root.Kinds.Where(k => k.Mime.Count > 0 || k.Extension.Count > 0).ToList()
            : [];
}
