using System.IO.Compression;

namespace app.module.archive.type.archive.kind.tar.gz;

/// <summary>A tar, gzip-compressed — <c>photos.tar.gz</c>.</summary>
public sealed class @this : tar.@this
{
    public @this() : base("tar.gz") { }

    public override string? Suffix => ".tar.gz";

    protected override System.IO.Stream Open(System.IO.Stream from) => new GZipStream(from, CompressionMode.Decompress);

    protected override System.IO.Stream Writer(System.IO.Stream into, global::app.module.archive.setting.Level level)
        => new GZipStream(into, compression.@this.Of(level), leaveOpen: true);
}
