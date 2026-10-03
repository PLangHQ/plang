using System.IO.Compression;

namespace app.module.archive.type.archive.kind.brotli;

/// <summary>brotli — <c>a.txt.br</c>; named or known by its suffix (it has no first bytes of its own).</summary>
public sealed class @this : compression.@this
{
    public @this() : base("brotli") { }

    public override string? Suffix => ".br";

    protected override System.IO.Stream Writer(System.IO.Stream into, CompressionLevel level) => new BrotliStream(into, level, leaveOpen: true);

    protected override System.IO.Stream Reader(System.IO.Stream from) => new BrotliStream(from, CompressionMode.Decompress);
}
