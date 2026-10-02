namespace app.type.item.size.kind.si;

/// <summary>SI — powers of 1000: <c>512 kB</c>, <c>100 MB</c>, <c>2 GB</c>. <c>KB</c> and <c>mb</c> read here too: their
/// suffix is this standard's, whatever its case.</summary>
public sealed class @this : kind.@this
{
    public @this() : base("si", 1000, "kB", "MB", "GB", "TB", "PB") { }
}
