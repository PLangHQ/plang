using System.Text;
using app.type;
using app.error;
using app.Utils;
using app.data;
using Verb = global::app.type.item.permission.Verb;

namespace app.type.item.path.file;

/// <summary>
/// FilePath verb implementations. Every method passes through <see cref="@this.AuthGate"/> (defined on the
/// base) first, then reaches the caller's filesystem (<c>context.Filesystem</c>) — the disk, or the overlay a
/// build checks its goals over. Same-scheme MoveTo/CopyTo override the base's naive default with the
/// filesystem's own Move/Copy and the bundled-consent prompt for fresh out-of-root pairs. Every verb acts as the
/// caller: its context carries the actor asked for permission and is the context the result Data is born with.
/// </summary>
public sealed partial class @this
{
    /// <summary>
    /// Loads a .NET assembly from this path. Gated by
    /// <see cref="@this.Authorize"/> on <c>Verb { Execute }</c> — Read grants
    /// do NOT cover Execute (Unix r/w/x model). The actor sees a separate
    /// "execute" prompt distinct from "read", so granting read access to a
    /// folder doesn't accidentally permit code loading from it.
    /// </summary>
    public override async Task<data.@this> LoadAssemblyAsync(actor.context.@this context)
    {
        if (await AuthGate(Verb.execute, context) is { } early)
            return early;
        if (!context.Filesystem.IsFile(this))
            return context.Error(new global::app.error.ServiceError($"Not found: {this}", "NotFound", 404));
        try
        {
            var asm = System.Reflection.Assembly.LoadFrom(Absolute);
            return context.Ok((object)asm);
        }
        catch (System.Exception ex) when (ex is System.IO.FileNotFoundException or System.IO.FileLoadException or System.BadImageFormatException)
        {
            return context.Error(new global::app.error.ServiceError($"Failed to load assembly: {ex.Message}", "AssemblyLoadFailed", 500));
        }
    }

    // --- Reads ---------------------------------------------------------------

    /// <summary>A file lands as a <c>file</c> reference, a folder as a <c>directory</c>; nothing there is
    /// NotFound (404) now, at the read, not at first touch. The stat tells which.</summary>
    public override async Task<data.@this> Read(actor.context.@this context, global::app.type.item.template.kind.@this? template = null)
    {
        // A failure, or an ask the gate suspends on (it exits the goal), is the read's answer as it is.
        var stat = await Stat(context);
        if (!stat.Success || stat.Exits) return stat;
        var info = await stat.Value();
        if (info is not { Exists: true })
            return context.Error(new ServiceError($"Not found: {this}", "NotFound", 404));
        // The reference is born through its type, so a program's `after file create` sees it.
        if (info.IsFile == false)
            return await context.App.type.list["directory"].Create(this, context, "directory");
        // born with its template: the file type makes the reference from this path, as it declares
        return await context.App.type.list[new global::app.type.@this("file", (string?)null, template: template), context]
            .Create(this, context, Name.ToString());
    }

    /// <summary>The <c>file</c> reference's type; a location with no known format expects nothing.</summary>
    public override Task<data.@this> Expect(actor.context.@this context)
        => Task.FromResult(Known(context) ? context.Ok(Reference("file", context)) : context.Ok());

    /// <summary>Why a file of a known format isn't there at build: missing, or the stat's own error when the
    /// build may not stat it — for a warning, never a failure: the read at run asks again under its own grant.
    /// Null when it is there, or its format is unknown.</summary>
    public override async Task<global::app.error.@this?> Absence(actor.context.@this context)
    {
        if (!Known(context)) return null;
        var exists = await Exists(context);
        if (!exists.Success) return exists.Error;
        return await exists.ToBooleanAsync() ? null
            : new global::app.error.@this($"'{this}' does not exist on disk", "NotFound", 404);
    }

    public override void Add(actor.context.@this context, global::app.type.item.path.@this? source = null)
        => context.Filesystem.Add(source is @this from ? Into(from, context.Filesystem) : this);

    public override void Remove(actor.context.@this context) => context.Filesystem.Remove(this);

    // Where a file sent here lands: under this location when it is a folder, else here.
    private @this Into(@this source, global::app.type.item.path.file.filesystem.@this filesystem)
        => filesystem.IsFolder(this) ? new @this(PathHelper.Combine(Absolute, source.Name.ToString()), Type) : this;

    // A location whose extension names a format — its kind carries extensions ({binary, xyz} for an unknown one carries none).
    private bool Known(actor.context.@this context) => Kind(context).kind.Extension.Count > 0;

    internal override async Task<data.@this<global::app.type.item.binary.@this>> Bytes(actor.context.@this context)
    {
        if (await AuthGate(Verb.read, context) is { } early) return data.@this<global::app.type.item.binary.@this>.From(early);
        if (!context.Filesystem.IsFile(this))
            return context.Error<global::app.type.item.binary.@this>(new global::app.error.ServiceError($"File not found: {Raw}", "NotFound", 404));
        try
        {
            return context.Ok<global::app.type.item.binary.@this>(new global::app.type.item.binary.@this(await context.Filesystem.Read(this), context.App.type.list[typeof(global::app.type.item.binary.@this)]));
        }
        catch (System.Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException)
        {
            return context.Error<global::app.type.item.binary.@this>(new global::app.error.ServiceError(ex.Message, "IOError", 500));
        }
    }

    public override async Task<data.@this<global::app.type.item.@bool.@this>> Exists(actor.context.@this context)
    {
        if (await AuthGate(Verb.read, context) is { } early) return data.@this<global::app.type.item.@bool.@this>.From(early);
        return context.Ok<global::app.type.item.@bool.@this>(context.Filesystem.IsFile(this) || context.Filesystem.IsFolder(this));
    }

    /// <summary>
    /// Truthiness of a file path is "does it exist". Routes through the gated
    /// <see cref="Exists"/> — the same shape as <c>HttpPath.AsBooleanAsync</c>:
    /// a denied or errored probe answers false. Keeps the existence check behind
    /// <see cref="@this.AuthGate"/> so an out-of-root probe still needs a Read
    /// grant (in-root is free via IsInRoot).
    /// </summary>
    public override async Task<bool> AsBooleanAsync(actor.context.@this context)
    {
        var existsResult = await Exists(context);
        return existsResult.Success && await existsResult.ToBooleanAsync();
    }

    /// <summary>
    /// List directory entries matching <paramref name="pattern"/>. Returns a
    /// list of FilePaths (Data&lt;list&lt;path&gt;&gt;). A file lists as itself when its name matches.
    /// </summary>
    public override async Task<data.@this<global::app.type.item.list.@this<global::app.type.item.path.@this>>> List(global::app.type.item.text.@this pattern, global::app.type.item.@bool.@this recursive, actor.context.@this context, Entry entry = Entry.file)
    {
        if (await AuthGate(Verb.read, context) is { } early) return data.@this<global::app.type.item.list.@this<global::app.type.item.path.@this>>.From(early);
        var listType = context.App.type.list[typeof(global::app.type.item.list.@this<global::app.type.item.path.@this>)];
        if (entry == Entry.file && context.Filesystem.IsFile(this))
            return context.Ok<global::app.type.item.list.@this<global::app.type.item.path.@this>>(
                new global::app.type.item.list.@this<global::app.type.item.path.@this>(
                    System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(pattern.ToString(), Name.ToString())
                        ? [new data.@this("", this, context: context)] : [], listType));
        if (!context.Filesystem.IsFolder(this))
            return context.Error<global::app.type.item.list.@this<global::app.type.item.path.@this>>(new global::app.error.ServiceError($"Directory not found: {Raw}", "NotFound", 404));
        try
        {
            // Each entry is this folder combined with its place under it — typed text included. An entry the gate
            // refuses (the stores' folder, and all under it) is left out; one it would ask about is listed, as named.
            var files = new List<data.@this>();
            foreach (var f in context.Filesystem.List(this, pattern.ToString(), recursive.Value)
                         .Where(entry == Entry.folder ? context.Filesystem.IsFolder : context.Filesystem.IsFile).ToList())
            {
                if (await f.Location(context.Filesystem).Standing(Verb.read, context) is { Success: false }) continue;
                files.Add(new data.@this("", Combine(f.Absolute[Absolute.Length..].TrimStart(PathHelper.DirectorySeparatorChar, PathHelper.AltDirectorySeparatorChar)),
                    context: context));
            }
            return context.Ok<global::app.type.item.list.@this<global::app.type.item.path.@this>>(
                new global::app.type.item.list.@this<global::app.type.item.path.@this>(files, listType));
        }
        catch (System.Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException)
        {
            return context.Error<global::app.type.item.list.@this<global::app.type.item.path.@this>>(new global::app.error.ServiceError(ex.Message, "IOError", 500));
        }
    }

    public override async Task<data.@this<global::app.type.item.path.@this.StatInfo>> Stat(actor.context.@this context)
    {
        if (await AuthGate(Verb.read, context) is { } early) return data.@this<global::app.type.item.path.@this.StatInfo>.From(early);
        return context.Ok<global::app.type.item.path.@this.StatInfo>(context.Filesystem.Stat(this, context));
    }

    // --- Writes --------------------------------------------------------------

    public override async Task<data.@this<global::app.type.item.path.@this>> WriteText(string content, actor.context.@this context)
    {
        if (await AuthGate(Verb.write, context) is { } early) return data.@this<global::app.type.item.path.@this>.From(early);
        await context.Filesystem.Write(this, Encoding.UTF8.GetBytes(content));
        return context.Ok<global::app.type.item.path.@this>(this);
    }

    /// <summary>
    /// The write door, the file reference's value door mirrored: the file's extension is a format, and the format
    /// writes the value (<c>.pr</c> → goal's, <c>.json</c> → json). A caller says where, never how. Returns
    /// the resulting Path wrapped in Data so the .pr's typed slot round-trips.
    /// </summary>
    public override async Task<data.@this<global::app.type.item.path.@this>> Save(data.@this? value, actor.context.@this context)
    {
        // The file is the channel the value is written to — it opens the value, its extension's format writes it.
        var answer = await new global::app.channel.type.file.@this(this, context)
            .Write(value ?? new data.@this("", context: context));
        return answer.Success && !answer.Exits
            ? context.Ok<global::app.type.item.path.@this>(this)
            : data.@this<global::app.type.item.path.@this>.From(answer);
    }

    public override async Task<data.@this<global::app.type.item.path.@this>> WriteBytes(byte[] content, actor.context.@this context)
    {
        if (await AuthGate(Verb.write, context) is { } early) return data.@this<global::app.type.item.path.@this>.From(early);
        await context.Filesystem.Write(this, content);
        return context.Ok<global::app.type.item.path.@this>(this);
    }

    /// <summary>Writes the file from <paramref name="content"/> as it is read — never held whole (a download's body) —
    /// gated as a write.</summary>
    public override async Task<data.@this<global::app.type.item.path.@this>> Write(System.IO.Stream content, actor.context.@this context)
    {
        if (await AuthGate(Verb.write, context) is { } early) return data.@this<global::app.type.item.path.@this>.From(early);
        await context.Filesystem.Write(this, content, context.CancellationToken);
        return context.Ok<global::app.type.item.path.@this>(this);
    }

    /// <summary>A file's bytes are its contents — read as they stream, gated as a read, never held whole.</summary>
    public override async Task<data.@this> Pour(System.IO.Stream into, actor.context.@this context)
    {
        if (await AuthGate(Verb.read, context) is { } early) return early;
        if (!context.Filesystem.IsFile(this))
            return context.Error(new global::app.error.ServiceError($"File not found: {Raw}", "NotFound", 404));
        try
        {
            await using var file = await context.Filesystem.Open(this);
            await file.CopyToAsync(into, context.CancellationToken);
            return context.Ok();
        }
        catch (System.Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException)
        {
            return context.Error(new global::app.error.ServiceError(ex.Message, "IOError", 500));
        }
    }

    /// <summary>A file packs as its contents, read as they stream (gated as a read), holding <c>file</c> with its name; a
    /// folder has no bytes of its own — a bundle packs it.</summary>
    internal override async Task<(data.@this result, string held, string? name)> Pack(data.@this self, System.IO.Stream into,
        actor.context.@this context)
    {
        if (context.Filesystem.IsFolder(this))
            return (context.Error(new global::app.error.ServiceError(
                $"{Raw} is a folder, and a folder has no bytes of its own: name a bundle — tar.gz, zip", "PackNeedsBundle", 400)), "file", null);
        return (await Pour(into, context), "file", Name.ToString());
    }

    /// <summary>The file, to read as it streams — gated as a read; or why it can't be (not allowed, not there). The
    /// caller disposes the stream.</summary>
    internal async Task<(System.IO.Stream? stream, data.@this? refused)> Open(actor.context.@this context)
    {
        if (await AuthGate(Verb.read, context) is { } early) return (null, early);
        if (!context.Filesystem.IsFile(this))
            return (null, context.Error(new global::app.error.ServiceError($"File not found: {Raw}", "NotFound", 404)));
        return (await context.Filesystem.Open(this), null);
    }

    /// <summary>Makes this a link leading to <paramref name="target"/>, as it is written — what is here is replaced
    /// (a link itself, never what it leads to). Gated as a write.</summary>
    internal async Task<data.@this<global::app.type.item.path.@this>> Link(string target, actor.context.@this context)
    {
        if (await AuthGate(Verb.write, context) is { } early) return data.@this<global::app.type.item.path.@this>.From(early);
        var filesystem = context.Filesystem;
        if (filesystem.Link(this) != null || filesystem.IsFile(this) || filesystem.IsFolder(this)) filesystem.Delete(this, recursive: true);
        filesystem.Link(this, target);
        return context.Ok<global::app.type.item.path.@this>(this);
    }

    /// <summary>Makes <paramref name="at"/>, inside this folder, a link leading where <paramref name="target"/> leads as
    /// this folder reads it (<see cref="Follow"/>: an absolute target from this folder, <c>..</c> never above it, each
    /// link on the way followed), written relative to the link's own folder: the OS, reading it from there, lands at the
    /// same place, inside (<c>bin/sh → /bin/busybox</c> is written <c>busybox</c>). Or why it can't be (not allowed, a
    /// link loop).</summary>
    internal async Task<data.@this<global::app.type.item.path.@this>> Link(@this at, string target, actor.context.@this context)
    {
        var folder = PathHelper.GetDirectoryName(at.Absolute) ?? Absolute;
        var from = PathHelper.GetRelativePath(Absolute, folder);
        // a relative target leads from the link's own folder; an absolute one from this folder
        var (spot, refused) = await Follow(target.StartsWith('/') || from == "." ? target : from + "/" + target, last: true, context);
        if (spot == null) return data.@this<global::app.type.item.path.@this>.From(refused!);
        return await at.Link(PathHelper.GetRelativePath(folder, spot.Absolute), context);
    }

    /// <summary>Sets who may read, write and run what is here, as someone running as themselves may: a setuid or setgid
    /// bit is dropped, and the owner always keeps read and write (and search on a folder), so it can be changed again.
    /// Gated as a write; nothing on a system with no such modes.</summary>
    internal async Task<data.@this<global::app.type.item.path.@this>> Mode(System.IO.UnixFileMode mode, actor.context.@this context)
    {
        if (await AuthGate(Verb.write, context) is { } early) return data.@this<global::app.type.item.path.@this>.From(early);
        if (OperatingSystem.IsWindows()) return context.Ok<global::app.type.item.path.@this>(this);
        var permissions = mode | System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite
                   | (context.Filesystem.IsFolder(this) ? System.IO.UnixFileMode.UserExecute : 0);
        context.Filesystem.Mode(this, permissions & ~(System.IO.UnixFileMode.SetUser | System.IO.UnixFileMode.SetGroup));
        return context.Ok<global::app.type.item.path.@this>(this);
    }

    /// <summary>
    /// Where <paramref name="relative"/> lands, taking this folder as the root: each link on the way is followed as the
    /// folder's own (an absolute one from this folder, never the host's root), and <c>..</c> never climbs above it — so
    /// nothing named under this folder is ever outside it. The last part is followed only when <paramref name="last"/>
    /// (a folder entries go into), never when it is what is written. Gated as a read of this folder; or why it can't be
    /// (not allowed, a link loop).
    /// </summary>
    internal async Task<(@this? at, data.@this? refused)> Follow(string relative, bool last, actor.context.@this context)
    {
        if (await AuthGate(Verb.read, context) is { } early) return (null, early);
        return Reach(relative, last, context.Filesystem) is { } at ? (at, null)
            : (null, context.Error(new global::app.error.ServiceError($"too many links resolving {relative} in {Raw}", "LinkLoop", 400)));
    }

    /// <summary>Where the OS lands for this path, read in <paramref name="filesystem"/>: from the host's root, each link on the
    /// way followed as the OS follows it (an absolute target from the host's root, a relative one from the link's own
    /// folder), this path's own last part too; null on a loop. Not gated: it reads only what links say.</summary>
    internal @this? Landing(global::app.type.item.path.file.filesystem.@this filesystem)
    {
        var root = PathHelper.GetPathRoot(Absolute);
        return string.IsNullOrEmpty(root) ? this : new @this(root, Type).Reach(Absolute[root.Length..], last: true, filesystem);
    }

    // what parts a path is written in: / everywhere, \ too on Windows (a legal name character elsewhere)
    private char[] Separators => OperatingSystem.IsWindows() ? ['/', '\\'] : ['/'];

    // relative from this folder as its root, each link on the way followed (the last part only when last), .. never
    // above this folder; null past 40 links. An absolute link starts again from this folder — but a walk from a drive's
    // root (the OS's reading) starts again from the link's own drive.
    private @this? Reach(string relative, bool last, global::app.type.item.path.file.filesystem.@this filesystem)
    {
        var separator = PathHelper.DirectorySeparatorChar;
        var host = string.Equals(PathHelper.GetPathRoot(Absolute), Absolute, RootComparison);
        var root = Absolute.TrimEnd(Separators);
        var parts = new Stack<string>(relative.Split(Separators, StringSplitOptions.RemoveEmptyEntries).Reverse());
        var at = new List<string>();
        var hops = 0;
        while (parts.Count > 0)
        {
            var part = parts.Pop();
            if (part == ".") continue;
            if (part == "..") { if (at.Count > 0) at.RemoveAt(at.Count - 1); continue; }
            var here = new @this(root + separator + string.Join(separator, at.Append(part)), Type);
            if ((parts.Count > 0 || last) && filesystem.Link(here) is { } link)
            {
                if (++hops > 40) return null;
                var linkRoot = PathHelper.GetPathRoot(link);
                // the link's parts go first; an absolute link starts again from this folder, or its own drive
                foreach (var linked in link[linkRoot.Length..].Split(Separators, StringSplitOptions.RemoveEmptyEntries).Reverse()) parts.Push(linked);
                if (linkRoot.Length > 0)
                {
                    at.Clear();
                    if (host && linkRoot.TrimEnd(Separators).Length > 0) root = linkRoot.TrimEnd(Separators);
                }
                continue;
            }
            at.Add(part);
        }
        return at.Count == 0 && root == Absolute.TrimEnd(Separators) ? this : new @this(root + separator + string.Join(separator, at), Type);
    }

    public override async Task<data.@this<global::app.type.item.path.@this>> Append(string content, actor.context.@this context)
    {
        if (await AuthGate(Verb.write, context) is { } early) return data.@this<global::app.type.item.path.@this>.From(early);
        await context.Filesystem.Append(this, content);
        return context.Ok<global::app.type.item.path.@this>(this);
    }

    public override async Task<data.@this<global::app.type.item.path.@this>> Mkdir(actor.context.@this context)
    {
        if (await AuthGate(Verb.write, context) is { } early) return data.@this<global::app.type.item.path.@this>.From(early);
        context.Filesystem.Create(this);
        return context.Ok<global::app.type.item.path.@this>(this);
    }

    // --- Destructive ---------------------------------------------------------

    /// <summary>
    /// Delete with file-action options. Non-recursive directory deletes refuse
    /// non-empty directories with <c>DirectoryNotEmpty</c>; a missing target is
    /// <c>NotFound</c>. Returns the resulting Path (post-delete) wrapped in Data so the
    /// caller can read <see cref="Exists"/> on it.
    /// </summary>
    public override async Task<data.@this<global::app.type.item.path.@this>> Delete(global::app.type.item.@bool.@this recursive, actor.context.@this context)
    {
        if (await AuthGate(Verb.delete, context) is { } early) return data.@this<global::app.type.item.path.@this>.From(early);
        try
        {
            var filesystem = context.Filesystem;
            if (filesystem.Link(this) != null || filesystem.IsFile(this))
                filesystem.Delete(this, recursive: false);
            else if (filesystem.IsFolder(this))
            {
                if (!recursive.Value && filesystem.List(this, "*", recursive: false).Any())
                    return context.Error<global::app.type.item.path.@this>(new global::app.error.ServiceError(
                        $"Directory is not empty: {Raw}. Use recursive=true to delete contents.", "DirectoryNotEmpty", 400));
                // all it holds is deleted with it: each entry asked of the gate before any is, then each removed alone,
                // children first — a folder is a plain remove, so an entry that appeared since the check fails it
                if (recursive.Value)
                {
                    var tree = Tree(null, subfolder: true, filesystem).ToList();
                    if (await Refusal(tree, Verb.delete, Verb.delete, null, context) is { } refused)
                        return data.@this<global::app.type.item.path.@this>.From(refused);
                    foreach (var (entry, _) in Enumerable.Reverse(tree)) filesystem.Delete(entry, recursive: false);
                }
                filesystem.Delete(this, recursive: false);
            }
            else
                return context.Error<global::app.type.item.path.@this>(new global::app.error.ServiceError($"Not found: {Raw}", "NotFound", 404));

            return context.Ok<global::app.type.item.path.@this>(this);
        }
        catch (System.Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException)
        {
            return context.Error<global::app.type.item.path.@this>(new global::app.error.ServiceError(ex.Message, "IOError", 500));
        }
    }

    // --- Same-scheme fast paths for Move/Copy --------------------------------

    /// <summary>
    /// Same-scheme move with action-level options. Bundled-consent for the
    /// out-of-root pair stays — calls into BundledTransfer with the overwrite
    /// option threaded through PerformTransfer. Cross-scheme moves fall
    /// through to the base default (Bytes → WriteBytes → Delete).
    /// </summary>
    public override async Task<data.@this<global::app.type.item.path.@this>> MoveTo(global::app.type.item.path.@this destination, global::app.type.item.@bool.@this overwrite, actor.context.@this context)
    {
        if (destination is not @this fileDest) return await base.MoveTo(destination, overwrite, context);
        return await BundledTransfer(fileDest, isMove: true, overwrite.Value, subfolder: true, context);
    }

    /// <summary>
    /// Same-scheme copy with action-level options. See <see cref="MoveTo"/>.
    /// </summary>
    public override async Task<data.@this<global::app.type.item.path.@this>> CopyTo(global::app.type.item.path.@this destination, global::app.type.item.@bool.@this overwrite, global::app.type.item.@bool.@this subfolder, actor.context.@this context)
    {
        if (destination is not @this fileDest) return await base.CopyTo(destination, overwrite, subfolder, context);
        return await BundledTransfer(fileDest, isMove: false, overwrite.Value, subfolder.Value, context);
    }

    /// <summary>
    /// Bundled-consent transfer. <paramref name="overwrite"/> and
    /// <paramref name="subfolder"/> are threaded through PerformTransfer
    /// so action-handler options (file.copy / file.move) ride along on the
    /// same bundled-prompt flow.
    /// </summary>
    private async Task<data.@this<global::app.type.item.path.@this>> BundledTransfer(@this destination, bool isMove, bool overwrite, bool subfolder, actor.context.@this context)
    {
        var sourceVerb = Verb.read;
        var destVerb   = Verb.write;
        var filesystem = context.Filesystem;
        // each end where the OS lands for it, worked out once for the whole transfer
        var source = Location(filesystem);
        var target = destination.Location(filesystem);

        // a move carries what the folder holds in one rename: a folder that holds the stores' folder's place is never
        // moved, whether the stores are there yet or not
        if (isMove && filesystem.store.IsUnder(source).Value)
            return context.Error<global::app.type.item.path.@this>(new global::app.error.PermissionDenied(source.Request(context.Actor, Verb.delete)));

        var sourceAuth = await source.Standing(sourceVerb, context);
        var destAuth   = await target.Standing(destVerb, context);

        // a side the gate refuses without asking (the stores' folder) is the answer, never asked
        if (sourceAuth is { Success: false }) return data.@this<global::app.type.item.path.@this>.From(sourceAuth);
        if (destAuth is { Success: false }) return data.@this<global::app.type.item.path.@this>.From(destAuth);

        // A folder's entries, walked once: each asked of the gate, both ends, before anything is touched — an entry it
        // refuses refuses the transfer whole; one it would ask is covered by the top's answer. The copy takes this walk.
        List<(@this Entry, @this? Into)>? tree;
        try
        {
            tree = filesystem.IsFolder(this) ? Tree(destination, subfolder || isMove, filesystem).ToList() : null;
        }
        catch (System.Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException)
        {
            return context.Error<global::app.type.item.path.@this>(new global::app.error.ServiceError(ex.Message, "IOError", 500));
        }
        if (tree != null && await Refusal(tree, sourceVerb, destVerb, target, context) is { } refused)
            return data.@this<global::app.type.item.path.@this>.From(refused);

        bool sourceOk = sourceAuth?.Success == true;
        bool destOk   = destAuth?.Success == true;

        if (sourceOk && destOk)
            return await PerformTransfer(destination, isMove, overwrite, tree, context);

        var question = new StringBuilder();
        question.Append(context.Actor!.Name).Append(" wants to:");
        if (!sourceOk) question.Append("\n  - read ").Append(Absolute);
        if (!destOk)   question.Append("\n  - write ").Append(destination.Absolute);
        question.Append("\n(y/n/a — covers all)");

        // the denial names the side not yet granted; an answer's grants are on the places asked about
        var request = !sourceOk ? source.Request(context.Actor, sourceVerb) : target.Request(context.Actor, destVerb);
        var consented = await context.Actor.Permission.Ask(question.ToString(), request, context, async persist =>
        {
            if (!sourceOk) await source.Keep(context.Actor, sourceVerb, persist, context);
            if (!destOk)   await target.Keep(context.Actor, destVerb, persist, context);
            return await PerformTransfer(destination, isMove, overwrite, tree, context);
        });
        return data.@this<global::app.type.item.path.@this>.From(consented);
    }

    // The entries a verb over this folder touches, each beside where it lands under to (null: nowhere — a delete): its
    // files, and its folders with all under them when subfolder; a folder before what it holds. Through the caller's
    // filesystem (the disk, or the build's overlay).
    private IEnumerable<(@this Entry, @this? Into)> Tree(@this? to, bool subfolder, global::app.type.item.path.file.filesystem.@this filesystem)
    {
        foreach (var entry in filesystem.List(this, "*", recursive: false).ToList())
        {
            var into = to == null ? null : new @this(PathHelper.Combine(to.Absolute, entry.Name.ToString()), to.Type);
            // a link is an entry of its own: never descended, never read through
            if (filesystem.Link(entry) != null || filesystem.IsFile(entry)) yield return (entry, into);
            else if (subfolder)
            {
                yield return (entry, into);
                foreach (var under in entry.Tree(into, subfolder, filesystem)) yield return under;
            }
        }
    }

    // The first entry the gate refuses, asked without asking: each entry for verb, where it lands for toward. Null when
    // none is refused — an entry it would ask is the top's answer's while it lands under the top's place (target); one a
    // link in the destination leads out of it is no part of what the top's answer covers, and without a grant of its own
    // is refused.
    private async Task<data.@this?> Refusal(IEnumerable<(@this Entry, @this? Into)> tree, Verb verb, Verb toward,
        global::app.type.item.path.@this? target, actor.context.@this context)
    {
        var filesystem = context.Filesystem;
        foreach (var (entry, into) in tree)
        {
            if (await entry.Location(filesystem).Standing(verb, context) is { Success: false } refused) return refused;
            if (into == null) continue;
            var place = into.Location(filesystem);
            var standing = await place.Standing(toward, context);
            if (standing is { Success: false }) return standing;
            var outside = target != null && !place.IsUnder(target).Value;
            if (standing == null && outside)
                return context.Error(new global::app.error.PermissionDenied(place.Request(context.Actor, toward)));
        }
        return null;
    }

    /// <summary>
    /// Performs the same-scheme transfer post-authorization. Handles both
    /// files and directories with action-handler options (overwrite, recursive
    /// subfolders) — absorbs <c>file/code/Default.cs::Default.Copy/Move</c>.
    /// Returns the new Path (post-transfer) wrapped in Data.
    /// </summary>
    private async Task<data.@this<global::app.type.item.path.@this>> PerformTransfer(@this destination, bool isMove, bool overwrite,
        List<(@this Entry, @this? Into)>? tree, actor.context.@this context)
    {
        var filesystem = context.Filesystem;
        // A folder copied whole: the entries its walk holds (its files, and its folders' when subfolder), each where it
        // lands — the entries the gate checked. A link is made again with its target as written, never read through.
        async Task Copy()
        {
            filesystem.Create(destination);
            foreach (var (entry, into) in tree!)
                if (filesystem.Link(entry) is { } target) Relink(into!, target);
                else if (filesystem.IsFile(entry)) await filesystem.Copy(entry, into!, overwrite);
                else filesystem.Create(into!);
        }
        // a link over what was there, as a copy of a file over one
        void Relink(@this at, string target)
        {
            if (filesystem.Link(at) != null || filesystem.IsFile(at)) filesystem.Delete(at, recursive: false);
            filesystem.Link(at, target);
        }
        try
        {
            if (!filesystem.IsFile(this) && !filesystem.IsFolder(this))
                return context.Error<global::app.type.item.path.@this>(new global::app.error.ServiceError($"Not found: {Raw}", "NotFound", 404));

            // Directory transfer ------------------------------------------------
            if (filesystem.IsFolder(this))
            {
                if (isMove) await filesystem.Move(this, destination, overwrite);
                else await Copy();
                return context.Ok<global::app.type.item.path.@this>(new @this(destination.Absolute, destination.Type) { Raw = destination.Raw });
            }

            // File transfer: into the destination when it names a folder ---------
            var target = destination.Into(this, filesystem);
            if (isMove) await filesystem.Move(this, target, overwrite);
            else await filesystem.Copy(this, target, overwrite);

            // The destination as given — or, when it named a folder, the file under it.
            var destTyped = ReferenceEquals(target, destination) ? destination.Raw : destination.Combine(Name.ToString()).Raw;
            return context.Ok<global::app.type.item.path.@this>(new @this(target.Absolute, target.Type) { Raw = destTyped });
        }
        catch (System.Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException)
        {
            return context.Error<global::app.type.item.path.@this>(new global::app.error.ServiceError(ex.Message, "IOError", 500));
        }
    }
}
