using System.Formats.Tar;

namespace app.module.archive.type.archive.kind.tar;

/// <summary>A tar — a folder's files, folders and links, one after another. Plain here; a compressed tar is its own
/// kind (<c>tar.gz</c>).</summary>
public class @this : bundle.@this
{
    public @this() : this("tar") { }

    protected @this(string name) : base(name) { }

    public override string? Suffix => ".tar";

    /// <summary>The tar <paramref name="from"/> holds — plain here; a compressed tar opens its compression first.</summary>
    protected virtual System.IO.Stream Open(System.IO.Stream from) => from;

    /// <summary>Where the tar is written into <paramref name="into"/> — plain here; a compressed tar compresses it at
    /// <paramref name="level"/>.</summary>
    protected virtual System.IO.Stream Writer(System.IO.Stream into, global::app.module.archive.setting.Level level) => into;

    protected override async IAsyncEnumerable<bundle.entry.@this> Entries(System.IO.Stream from,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await using var reader = new TarReader(Open(from));
        while (await reader.GetNextEntryAsync(copyData: false, ct) is { } entry)
            yield return new bundle.entry.@this(entry.Name, entry.EntryType switch
            {
                TarEntryType.Directory => bundle.entry.Kind.Folder,
                TarEntryType.RegularFile or TarEntryType.V7RegularFile or TarEntryType.ContiguousFile => bundle.entry.Kind.File,
                TarEntryType.SymbolicLink => bundle.entry.Kind.Link,
                TarEntryType.HardLink => bundle.entry.Kind.HardLink,
                _ => bundle.entry.Kind.Other,
            }, entry.Mode, entry.LinkName, entry.DataStream);
    }

    protected override async Task Write(System.IO.Stream into, IAsyncEnumerable<bundle.entry.@this> entries,
        global::app.module.archive.setting.Level level, CancellationToken ct)
    {
        var written = Writer(into, level);
        await using (var writer = new TarWriter(written, TarEntryFormat.Pax, leaveOpen: true))
            await foreach (var entry in entries.WithCancellation(ct))
            {
                var tarred = new PaxTarEntry(entry.Kind switch
                {
                    bundle.entry.Kind.Folder => TarEntryType.Directory,
                    bundle.entry.Kind.Link => TarEntryType.SymbolicLink,
                    _ => TarEntryType.RegularFile,
                }, entry.Kind == bundle.entry.Kind.Folder ? entry.Name + "/" : entry.Name) { Mode = entry.Mode, DataStream = entry.Content };
                // only a link names what it leads to
                if (entry.Kind == bundle.entry.Kind.Link) tarred.LinkName = entry.Target ?? "";
                await writer.WriteEntryAsync(tarred, ct);
            }
        if (!ReferenceEquals(written, into)) await written.DisposeAsync();
    }
}
