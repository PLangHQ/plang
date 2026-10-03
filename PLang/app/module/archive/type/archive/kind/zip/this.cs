using System.IO.Compression;

namespace app.module.archive.type.archive.kind.zip;

/// <summary>A zip — a folder's files and folders, each compressed on its own. A link is kept as a link when the zip
/// says its entry is one.</summary>
public sealed class @this : bundle.@this
{
    // the file-type bits of a unix mode, and a link's
    private const int Type = 0xF000, Link = 0xA000;

    public @this() : base("zip") { }

    public override string? Suffix => ".zip";

    public override bool Reads(ReadOnlySpan<byte> start) => start is [0x50, 0x4b, 0x03, 0x04, ..];

    protected override async IAsyncEnumerable<bundle.entry.@this> Entries(System.IO.Stream from,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await using var zip = new ZipArchive(from, ZipArchiveMode.Read, leaveOpen: true);
        foreach (var entry in zip.Entries)
        {
            ct.ThrowIfCancellationRequested();
            var mode = (entry.ExternalAttributes >> 16) & 0xFFFF;
            var folder = entry.FullName.EndsWith('/');
            if (!folder && (mode & Type) == Link)
            {
                using var target = new System.IO.StreamReader(entry.Open());
                yield return new bundle.entry.@this(entry.FullName, bundle.entry.Kind.Link, Mode(mode), await target.ReadToEndAsync(ct));
                continue;
            }
            await using var content = folder ? null : entry.Open();
            yield return new bundle.entry.@this(entry.FullName, folder ? bundle.entry.Kind.Folder : bundle.entry.Kind.File,
                Mode(mode), content: content);
        }
    }

    protected override async Task Write(System.IO.Stream into, IAsyncEnumerable<bundle.entry.@this> entries,
        global::app.module.archive.setting.Level level, CancellationToken ct)
    {
        await using var zip = new ZipArchive(into, ZipArchiveMode.Create, leaveOpen: true);
        await foreach (var entry in entries.WithCancellation(ct))
        {
            var written = zip.CreateEntry(entry.Kind == bundle.entry.Kind.Folder ? entry.Name + "/" : entry.Name, compression.@this.Of(level));
            written.ExternalAttributes = ((int)entry.Mode | (entry.Kind == bundle.entry.Kind.Link ? Link : 0)) << 16;
            if (entry.Kind == bundle.entry.Kind.Folder) continue;
            await using var to = written.Open();
            if (entry.Kind == bundle.entry.Kind.Link) await to.WriteAsync(System.Text.Encoding.UTF8.GetBytes(entry.Target ?? ""), ct);
            else if (entry.Content != null) await entry.Content.CopyToAsync(to, ct);
        }
    }

    // the permission bits of a zip entry's unix mode; none written is a file the owner may read and write
    private System.IO.UnixFileMode Mode(int mode) => (System.IO.UnixFileMode)((mode & 0xFFF) is var bits and > 0 ? bits : 0b110_100_100);
}
