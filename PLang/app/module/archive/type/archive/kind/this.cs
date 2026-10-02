namespace app.module.archive.type.archive.kind;

/// <summary>
/// A format an archive is in — a kind of <c>archive</c>: a compression (<c>gzip</c>, <c>deflate</c>, <c>brotli</c>) that
/// holds one value or file, or a bundle (<c>tar</c>, <c>tar.gz</c>, <c>zip</c>, <c>oci.layer</c>) that holds a folder.
/// Each packs and unpacks itself; a new format is one new folder.
/// </summary>
public abstract class @this : global::app.type.kind.@this
{
    protected @this(string name) : base(name) { }

    protected internal override string Owner => "archive";

    /// <summary>What a file in this format is named with at its end (<c>.tar.gz</c>) — how a file says its format; null
    /// when none says it.</summary>
    public virtual string? Suffix => null;

    /// <summary>Whether bytes beginning with <paramref name="start"/> are in this format — what an archive named by
    /// nothing is known by.</summary>
    public virtual bool Reads(ReadOnlySpan<byte> start) => false;

    /// <summary>Packs <paramref name="value"/> (its Data, <paramref name="self"/>) into <paramref name="into"/> at
    /// <paramref name="level"/> — answers what it holds, or why it can't.</summary>
    internal abstract Task<(global::app.data.@this result, held.@this? held)> Pack(global::app.data.@this self,
        global::app.type.item.@this value, System.IO.Stream into, setting.Level level, global::app.actor.context.@this context);

    /// <summary>Unpacks <paramref name="from"/>, holding <paramref name="held"/>, into the folder
    /// <paramref name="into"/> — or, holding a Data and no folder named, gives the Data back. At most
    /// <paramref name="max"/> is written.</summary>
    internal abstract Task<global::app.data.@this> Unpack(System.IO.Stream from, held.@this? held,
        global::app.type.item.path.file.@this? into, global::app.type.item.size.@this max, global::app.actor.context.@this context);

    /// <summary>Where <paramref name="name"/> lands in the folder <paramref name="into"/>: its folders followed as
    /// <paramref name="into"/>'s own, <c>..</c> never above it, the last part itself never followed. Nothing — no place
    /// and no refusal — when the name is the folder itself (<c>..</c>, <c>a/..</c>): nothing is written over the folder
    /// an unpack goes into.</summary>
    protected async Task<(global::app.type.item.path.file.@this? at, global::app.data.@this? refused)> Under(
        global::app.type.item.path.file.@this into, string name, global::app.actor.context.@this context)
    {
        var (at, refused) = await into.Follow(name, last: false, context);
        return at == null ? (null, refused) : at.Absolute.TrimEnd('/') == into.Absolute.TrimEnd('/') ? (null, null) : (at, null);
    }

    /// <summary>Whatever is at <paramref name="at"/> — a file, a link (never what it leads to), a folder with what it
    /// holds — gone, so what is unpacked there is written in its place, never through a link; nothing there is nothing
    /// to do.</summary>
    protected async Task Clear(global::app.type.item.path.file.@this at, global::app.actor.context.@this context)
    {
        var files = context.FileSystem;
        if (files.Link(at) != null || files.IsFile(at) || files.IsFolder(at))
            await at.Delete((global::app.type.item.@bool.@this)true, context);
    }
}
