using System.IO.Compression;

namespace app.module.archive.type.archive.kind.gzip;

/// <summary>gzip — <c>a.txt.gz</c>.</summary>
public sealed class @this : compression.@this
{
    public @this() : base("gzip") { }

    public override string? Suffix => ".gz";

    public override bool Reads(ReadOnlySpan<byte> start) => start is [0x1f, 0x8b, ..];

    protected override System.IO.Stream Writer(System.IO.Stream into, CompressionLevel level) => new GZipStream(into, level, leaveOpen: true);

    protected override System.IO.Stream Reader(System.IO.Stream from) => new GZipStream(from, CompressionMode.Decompress);
}
