using app.module.archive.code;

namespace app.module.archive;

[Action("pack", Cacheable = false)]
public partial class pack : IContext
{
    /// <summary>What to pack — the value decides what that is: a value packs as its Data whole (unpacking gives the Data
    /// back), a file as its contents and its name, a folder into a bundle (tar.gz, zip).</summary>
    [IsNotNull, Whole]
    public partial data.@this Value { get; init; }

    /// <summary>The format — a compression (gzip, deflate, brotli) or a bundle (tar, tar.gz, zip, oci.layer). Left out,
    /// the one <see cref="To"/>'s name ends in, else gzip. A folder's format is always named, by this or by To.</summary>
    public partial data.@this<global::app.type.item.choice.@this<global::app.module.archive.type.archive.kind.@this>>? Format { get; init; }

    /// <summary>Where the archive is written as it is packed — the answer is then this path, and the archive is never
    /// held whole. Left out, the archive is the answer.</summary>
    public partial data.@this<global::app.type.item.path.@this>? To { get; init; }

    /// <summary>How hard it compresses — fastest, optimal, smallest or none; left out, <c>%!archive.setting.level%</c>.</summary>
    public partial data.@this<global::app.type.item.choice.@this<global::app.module.archive.setting.Level>>? Level { get; init; }

    [Code]
    public partial IArchive Archive { get; }

    public async Task<data.@this> Start() => await Archive.Pack(this);
}
