using System.Formats.Tar;

namespace app.module.archive.type.archive.kind.tar;

/// <summary>A tar — a folder's files, folders and links, one after another. Read plain; a compressed tar is its own
/// kind (<c>tar.gz</c>).</summary>
public class @this : bundle.@this
{
    public @this() : this("tar") { }

    protected @this(string name) : base(name) { }

    /// <summary>The tar <paramref name="from"/> holds — plain here; a compressed tar opens its compression first.</summary>
    protected virtual System.IO.Stream Open(System.IO.Stream from) => from;

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
}
