using File = global::app.type.item.path.file.@this;
using Kind = global::app.module.archive.type.archive.kind.bundle.entry.Kind;

namespace app.module.archive.type.archive.kind.bundle;

/// <summary>
/// A bundle — a folder held in one archive (<c>tar</c>, <c>zip</c>, <c>oci.layer</c>). Each format reads its own entries;
/// what an entry may do to the folder it unpacks into is decided here, once, for every format. Nothing in it lands
/// outside that folder: a name with <c>..</c> stays inside, a link in the folder is followed as the folder's own (an
/// absolute one from the folder, never the host's root), a file written over a link replaces the link, a hard link is a
/// copy of a file inside or nothing. Devices and fifos are never made; a setuid or setgid bit is dropped and the owner
/// always keeps read and write. The whole unpack writes at most its cap. Every read and write goes through the folder's
/// gated verbs.
/// </summary>
public abstract class @this : kind.@this
{
    protected @this(string name) : base(name) { }

    /// <summary>The entries <paramref name="from"/> holds, in order; each entry's content is read before the next is
    /// asked for.</summary>
    protected abstract IAsyncEnumerable<entry.@this> Entries(System.IO.Stream from, CancellationToken ct);

    /// <summary>Unpacks <paramref name="from"/> into the folder <paramref name="into"/>, writing at most
    /// <paramref name="max"/> — gated as a write there. Answers the folder, or why it couldn't.</summary>
    internal async Task<global::app.data.@this> Unpack(System.IO.Stream from, File into, global::app.type.item.size.@this max,
        global::app.actor.context.@this context)
    {
        var made = await into.Mkdir(context);
        if (!made.Success) return made;
        var cap = new cap.@this(max);
        try
        {
            await foreach (var entry in Entries(from, context.CancellationToken))
            {
                if (entry.Name.Length == 0) continue;
                var landed = await Land(entry, into, cap, context);
                if (!landed.Success) return landed;
            }
            return context.Ok<global::app.type.item.path.@this>(into);
        }
        catch (global::app.error.AppException refused)
        {
            return context.Error(refused.Error);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or FormatException or UnauthorizedAccessException)
        {
            return context.Error(new global::app.error.ServiceError($"Could not unpack {Name} into {into.Raw}: {ex.Message}", "UnpackFailed", 500));
        }
    }

    /// <summary>One entry, landed where it belongs inside <paramref name="into"/>. A format whose entries mean more
    /// (a layer's whiteouts) answers them first.</summary>
    protected virtual async Task<global::app.data.@this> Land(entry.@this entry, File into, cap.@this cap,
        global::app.actor.context.@this context)
    {
        var (at, refused) = await Under(into, entry.Folder, entry.Leaf, context);
        if (at == null) return refused!;
        switch (entry.Kind)
        {
            case Kind.Folder:
                if (context.FileSystem.Link(at) != null) await at.Delete(context);
                var folder = await at.Mkdir(context);
                return folder.Success ? await at.Mode(entry.Mode, context) : folder;
            case Kind.File:
                await Clear(at, context);
                var written = entry.Content == null ? await at.WriteBytes([], context) : await at.Write(cap.Over(entry.Content), context);
                return written.Success ? await at.Mode(entry.Mode, context) : written;
            case Kind.Link:
                return await at.Link(entry.Target ?? "", context);
            case Kind.HardLink:
                // a copy of the file it names inside the folder — a hard link needs link(2); a copy is the same bytes
                var (target, unfollowed) = await into.Follow(entry.Target ?? "", last: true, context);
                if (target == null) return unfollowed!;
                await Clear(at, context);
                if (!context.FileSystem.IsFile(target)) return context.Ok();
                return await target.CopyTo(at, (global::app.type.item.@bool.@this)true, (global::app.type.item.@bool.@this)false, context);
            default:
                // devices, fifos and the rest: never made by someone unpacking as themselves
                return context.Ok();
        }
    }

    /// <summary>Where <paramref name="leaf"/> lands in <paramref name="folder"/>, a folder of <paramref name="into"/>:
    /// the folder followed as <paramref name="into"/>'s own, the leaf itself never followed.</summary>
    protected async Task<(File? at, global::app.data.@this? refused)> Under(File into, string folder, string leaf,
        global::app.actor.context.@this context)
    {
        var (parent, refused) = await into.Follow(folder, last: true, context);
        return parent == null ? (null, refused) : (new File(parent.Absolute.TrimEnd('/') + "/" + leaf), null);
    }

    /// <summary>Whatever is at <paramref name="at"/> — a file, a link (never what it leads to), a folder with what it
    /// holds — gone; nothing there is nothing to do.</summary>
    protected async Task Clear(File at, global::app.actor.context.@this context)
    {
        var files = context.FileSystem;
        if (files.Link(at) != null || files.IsFile(at) || files.IsFolder(at))
            await at.Delete((global::app.type.item.@bool.@this)true, context);
    }
}
