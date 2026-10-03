using app.module.archive.code;

namespace app.module.archive;

[Action("unpack", Cacheable = false)]
public partial class unpack : IContext
{
    /// <summary>The archive — what pack answered, or a file in a format (<c>/backup/photos.tar.gz</c>).</summary>
    [IsNotNull]
    public partial data.@this<global::app.module.archive.type.archive.@this> Value { get; init; }

    /// <summary>The folder it unpacks into — a bundle, or an archived file, lands there; nothing of it outside. Left out,
    /// an archived Data comes back as the Data.</summary>
    public partial data.@this<global::app.type.item.path.@this>? Into { get; init; }

    /// <summary>The format, when the archive doesn't say it — a layer of a container image is <c>oci.layer</c>. Left
    /// out, the archive's own, else what its name ends in, else what its first bytes are.</summary>
    public partial data.@this<global::app.type.item.choice.@this<global::app.module.archive.type.archive.kind.@this>>? Format { get; init; }

    /// <summary>The most it may unpack to (500 MB, 2 GiB) — an archive that unpacks to more is refused as it passes it.
    /// Left out, <c>%!archive.setting.max%</c>.</summary>
    public partial data.@this<global::app.type.item.size.@this>? Max { get; init; }

    [Code]
    public partial IArchive Archive { get; }

    public async Task<data.@this> Start() => await Archive.Unpack(this);
}
