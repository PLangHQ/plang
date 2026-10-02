using System.IO.Compression;

namespace app.module.archive.type.archive.kind.deflate;

/// <summary>deflate — raw, with no header; named (<c>as deflate</c>), never known by its bytes.</summary>
public sealed class @this : compression.@this
{
    public @this() : base("deflate") { }

    protected override System.IO.Stream Writer(System.IO.Stream into, CompressionLevel level) => new DeflateStream(into, level, leaveOpen: true);

    protected override System.IO.Stream Reader(System.IO.Stream from) => new DeflateStream(from, CompressionMode.Decompress);
}
