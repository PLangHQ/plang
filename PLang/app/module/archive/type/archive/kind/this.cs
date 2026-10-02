namespace app.module.archive.type.archive.kind;

/// <summary>
/// A format an archive is in — a kind of <c>archive</c>: a compression (<c>gzip</c>, <c>deflate</c>, <c>brotli</c>) that
/// holds one value, or a bundle (<c>tar</c>, <c>tar.gz</c>, <c>zip</c>, <c>oci.layer</c>) that holds a folder. Each packs
/// and unpacks itself; a new format is one new folder.
/// </summary>
public abstract class @this : global::app.type.kind.@this
{
    protected @this(string name) : base(name) { }

    protected internal override string Owner => "archive";
}
