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

    /// <summary>A layer is a tar, gzip-compressed when its first bytes say so.</summary>
    protected override System.IO.Stream Open(System.IO.Stream from)
    {
        var magic = new byte[2];
        var read = from.ReadAtLeast(magic, 2, throwOnEndOfStream: false);
        var whole = new joined(new System.IO.MemoryStream(magic, 0, read), from);
        return read == 2 && magic[0] == 0x1f && magic[1] == 0x8b
            ? new GZipStream(whole, CompressionMode.Decompress)
            : whole;
    }

    protected override async Task<global::app.data.@this> Land(bundle.entry.@this entry, File into, bundle.cap.@this cap,
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
        await Clear(new File(folder.Absolute.TrimEnd('/') + "/" + entry.Leaf[Whiteout.Length..]), context);
        return context.Ok();
    }

    // the bytes peeked at, then the rest of the stream
    private sealed class joined(System.IO.Stream first, System.IO.Stream rest) : System.IO.Stream
    {
        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = first.Read(buffer, offset, count);
            return read > 0 ? read : rest.Read(buffer, offset, count);
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            var read = await first.ReadAsync(buffer, ct);
            return read > 0 ? read : await rest.ReadAsync(buffer, ct);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new System.NotSupportedException();
        public override long Position { get => throw new System.NotSupportedException(); set => throw new System.NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, System.IO.SeekOrigin origin) => throw new System.NotSupportedException();
        public override void SetLength(long value) => throw new System.NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new System.NotSupportedException();
    }
}
