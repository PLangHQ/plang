using System.IO.Compression;
using File = global::app.type.item.path.file.@this;

namespace app.module.archive.type.archive.kind.oci.layer;

/// <summary>
/// A container image's layer — a tar, plain or gzip, unpacked over the layers before it. Its whiteouts remove what
/// earlier layers put there: <c>.wh.x</c> removes x, <c>.wh..wh..opq</c> empties its folder of what came before (this
/// layer's own entries come after). A whiteout itself never lands.
/// </summary>
public sealed class @this : tar.@this
{
    private const string Whiteout = ".wh.", Opaque = ".wh..wh..opq";

    public @this() : base("oci.layer") { }

    /// <summary>A layer has no suffix of its own: it is named (<c>as oci.layer</c>).</summary>
    public override string? Suffix => null;

    /// <summary>A layer is a tar, gzip-compressed when its first bytes say so.</summary>
    protected override System.IO.Stream Open(System.IO.Stream from)
    {
        var start = new code.peeked(from);
        return start.Start is [0x1f, 0x8b, ..] ? new GZipStream(start, CompressionMode.Decompress) : start;
    }

    protected override async Task<global::app.data.@this> Land(bundle.entry.@this entry, File into, cap.@this cap,
        global::app.actor.context.@this context)
    {
        if (!entry.Leaf.StartsWith(Whiteout, StringComparison.Ordinal)) return await base.Land(entry, into, cap, context);
        var (folder, refused) = await into.Follow(entry.Folder, last: true, context);
        if (folder == null) return refused!;
        if (entry.Leaf == Opaque)
        {
            if (!context.FileSystem.IsFolder(folder)) return context.Ok();
            var listed = await folder.List(context);
            if (!listed.Success) return listed;
            foreach (var child in (await listed.Value())!.Items())
                if (child is File gone) await Clear(gone, context);
            return context.Ok();
        }
        var (removed, unfollowed) = await Under(folder, entry.Leaf[Whiteout.Length..], context);
        if (removed == null) return unfollowed ?? context.Ok();
        await Clear(removed, context);
        return context.Ok();
    }
}
