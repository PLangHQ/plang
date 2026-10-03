using File = global::app.type.item.path.file.@this;
using Kind = global::app.module.archive.type.archive.kind.bundle.entry.Kind;

namespace app.module.archive.type.archive.kind.bundle;

/// <summary>
/// A bundle — a folder held in one archive (<c>tar</c>, <c>tar.gz</c>, <c>zip</c>, <c>oci.layer</c>). Each format reads
/// and writes its own entries; what an entry may do to the folder it unpacks into is decided here, once, for every
/// format. Nothing in it lands outside that folder: a name with <c>..</c> stays inside, a link in the folder is followed
/// as the folder's own (an absolute one from the folder, never the host's root), a file written over a link replaces
/// the link, a hard link is a copy of a file inside or nothing, nothing is written over the folder itself. Devices and
/// fifos are never made; a setuid or setgid bit is dropped and the owner always keeps read and write. The whole unpack
/// writes at most its cap. Every read and write goes through the folder's gated verbs.
/// </summary>
public abstract class @this : kind.@this
{
    // the mode a packed entry is given: a folder anyone may enter, a file its owner may write
    private const System.IO.UnixFileMode Folder = (System.IO.UnixFileMode)0b111_101_101, File_ = (System.IO.UnixFileMode)0b110_100_100;

    protected @this(string name) : base(name) { }

    /// <summary>The entries <paramref name="from"/> holds, in order; each entry's content is read before the next is
    /// asked for.</summary>
    protected abstract IAsyncEnumerable<entry.@this> Entries(System.IO.Stream from, CancellationToken ct);

    /// <summary>Writes <paramref name="entries"/> into <paramref name="into"/>, in this format, at
    /// <paramref name="level"/>.</summary>
    protected abstract Task Write(System.IO.Stream into, IAsyncEnumerable<entry.@this> entries,
        global::app.module.archive.setting.Level level, CancellationToken ct);

    /// <summary>A bundle packs a folder — the value read as a path (<c>/photos</c> names it) — its files, folders and
    /// links, each read through its gate, holding the folder by its name.</summary>
    internal override async Task<(global::app.data.@this result, held.@this? held)> Pack(global::app.data.@this self,
        global::app.type.item.@this value, System.IO.Stream into, global::app.module.archive.setting.Level level,
        global::app.actor.context.@this context)
    {
        var folder = await self.As<global::app.type.item.path.@this>().Value() as File;
        if (folder == null || !context.FileSystem.IsFolder(folder))
            return (context.Error(new global::app.error.ServiceError(
                $"a {Name} packs a folder, and %{self.Name}% is none", "PackNeedsFolder", 400)), null);
        var listed = await folder.List((global::app.type.item.text.@this)"*", (global::app.type.item.@bool.@this)true, context);
        if (!listed.Success) return (listed, null);
        try
        {
            await Write(into, Walk(folder, (await listed.Value())!.Items(), context), level, context.CancellationToken);
            return (context.Ok(), new held.@this("folder", folder.Name.ToString()));
        }
        catch (global::app.error.AppException refused)
        {
            return (context.Error(refused.Error), null);
        }
    }

    // What a folder holds, as entries named from it: a link as its target, a folder, a file with its content opened
    // through its gate as it is reached.
    private async IAsyncEnumerable<entry.@this> Walk(File folder, IEnumerable<global::app.type.item.path.@this> children,
        global::app.actor.context.@this context)
    {
        var root = folder.Absolute.TrimEnd('/') + "/";
        foreach (var child in children)
        {
            if (child is not File at || !at.Absolute.StartsWith(root, StringComparison.Ordinal)) continue;
            var name = at.Absolute[root.Length..];
            if (context.FileSystem.Link(at) is { } target)
                yield return new entry.@this(name, Kind.Link, File_, target);
            else if (context.FileSystem.IsFolder(at))
                yield return new entry.@this(name, Kind.Folder, Folder);
            else
            {
                var (content, refused) = await at.Open(context);
                if (content == null) throw new global::app.error.AppException(refused!.Error!);
                await using (content)
                    yield return new entry.@this(name, Kind.File, File_, content: content);
            }
        }
    }

    /// <summary>Unpacks <paramref name="from"/> into the folder <paramref name="into"/>, writing at most
    /// <paramref name="max"/> — gated as a write there. A bundle unpacks into a folder, never into a value. Answers the
    /// folder, or why it couldn't.</summary>
    internal override async Task<global::app.data.@this> Unpack(System.IO.Stream from, held.@this? held, File? into,
        global::app.type.item.size.@this max, global::app.actor.context.@this context)
    {
        if (into == null)
            return context.Error(new global::app.error.ServiceError(
                $"a {Name} unpacks into a folder: say where (unpack … into /folder)", "UnpackNeedsFolder", 400));
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
        var (at, refused) = await Under(into, entry.Name, context);
        if (at == null) return refused ?? context.Ok();
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
}
