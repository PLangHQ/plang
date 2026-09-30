using System.Text;
using app.type;
using app.error;
using app.Utils;
using app.data;
using Verb = global::app.type.item.permission.Verb;

namespace app.type.item.path.file;

/// <summary>
/// FilePath verb implementations. Every method passes through <see cref="@this.AuthGate"/> (defined on the
/// base) first, then reaches the caller's filesystem (<c>context.FileSystem</c>) — the disk, or the overlay a
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
        if (await AuthGate(Verb.Execute, context) is { } early)
            return early;
        if (!context.FileSystem.IsFile(this))
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
    public override async Task<data.@this> Read(actor.context.@this context, global::app.type.item.@bool.@this? template = null)
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
        var marked = Marked(template);
        return await context.App.type.list[new global::app.type.@this("file", (string?)null, template: marked), context]
            .Create(this, context, FileName);
    }

    /// <summary>The <c>file</c> reference's type; a location with no known format expects nothing.</summary>
    public override Task<data.@this> Expect(actor.context.@this context)
        => Task.FromResult(Known(context) ? context.Ok(Reference("file", context)) : context.Ok());

    /// <summary>Why a file of a known format isn't there at build: missing, or the stat's own error when the
    /// build may not stat it — for a warning, never a failure: the read at run asks again under its own grant.
    /// Null when it is there, or its format is unknown.</summary>
    public override async Task<global::app.error.Error?> Absence(actor.context.@this context)
    {
        if (!Known(context)) return null;
        var exists = await ExistsAsync(context);
        if (!exists.Success) return exists.Error;
        return await exists.ToBooleanAsync() ? null
            : new global::app.error.Error($"'{this}' does not exist on disk", "NotFound", 404);
    }

    // A location whose extension names a format — its kind carries extensions ({binary, xyz} for an unknown one carries none).
    private bool Known(actor.context.@this context) => Kind(context).kind.Extension.Count > 0;

    internal override async Task<data.@this<global::app.type.item.binary.@this>> Bytes(actor.context.@this context)
    {
        if (await AuthGate(Verb.Read, context) is { } early) return data.@this<global::app.type.item.binary.@this>.From(early);
        if (!context.FileSystem.IsFile(this))
            return context.Error<global::app.type.item.binary.@this>(new global::app.error.ServiceError($"File not found: {Raw}", "NotFound", 404));
        try
        {
            return context.Ok<global::app.type.item.binary.@this>(new global::app.type.item.binary.@this(await context.FileSystem.Read(this)));
        }
        catch (System.Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException)
        {
            return context.Error<global::app.type.item.binary.@this>(new global::app.error.ServiceError(ex.Message, "IOError", 500));
        }
    }

    public override async Task<data.@this<global::app.type.item.@bool.@this>> ExistsAsync(actor.context.@this context)
    {
        if (await AuthGate(Verb.Read, context) is { } early) return data.@this<global::app.type.item.@bool.@this>.From(early);
        return context.Ok<global::app.type.item.@bool.@this>(context.FileSystem.IsFile(this) || context.FileSystem.IsFolder(this));
    }

    /// <summary>
    /// Truthiness of a file path is "does it exist". Routes through the gated
    /// <see cref="ExistsAsync"/> — the same shape as <c>HttpPath.AsBooleanAsync</c>:
    /// a denied or errored probe answers false. Keeps the existence check behind
    /// <see cref="@this.AuthGate"/> so an out-of-root probe still needs a Read
    /// grant (in-root is free via IsInRoot).
    /// </summary>
    public override async Task<bool> AsBooleanAsync(actor.context.@this context)
    {
        var existsResult = await ExistsAsync(context);
        return existsResult.Success && await existsResult.ToBooleanAsync();
    }

    /// <summary>
    /// List directory entries matching <paramref name="pattern"/>. Returns a
    /// list of FilePaths (Data&lt;list&lt;path&gt;&gt;).
    /// </summary>
    public override async Task<data.@this<global::app.type.item.list.@this<global::app.type.item.path.@this>>> List(global::app.type.item.text.@this pattern, global::app.type.item.@bool.@this recursive, actor.context.@this context)
    {
        if (await AuthGate(Verb.Read, context) is { } early) return data.@this<global::app.type.item.list.@this<global::app.type.item.path.@this>>.From(early);
        if (!context.FileSystem.IsFolder(this))
            return context.Error<global::app.type.item.list.@this<global::app.type.item.path.@this>>(new global::app.error.ServiceError($"Directory not found: {Raw}", "NotFound", 404));
        try
        {
            // Each file is this folder combined with its place under it — typed text included.
            var files = context.FileSystem.List(this, pattern.ToString(), recursive.Value).Where(context.FileSystem.IsFile)
                .Select(f => new data.@this("", Combine(f.Absolute[Absolute.Length..].TrimStart(PathHelper.DirectorySeparatorChar, PathHelper.AltDirectorySeparatorChar)),
                    context: context))
                .ToList();
            return context.Ok<global::app.type.item.list.@this<global::app.type.item.path.@this>>(
                new global::app.type.item.list.@this<global::app.type.item.path.@this>(files));
        }
        catch (System.Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException)
        {
            return context.Error<global::app.type.item.list.@this<global::app.type.item.path.@this>>(new global::app.error.ServiceError(ex.Message, "IOError", 500));
        }
    }

    public override async Task<data.@this<global::app.type.item.path.@this.StatInfo>> Stat(actor.context.@this context)
    {
        if (await AuthGate(Verb.Read, context) is { } early) return data.@this<global::app.type.item.path.@this.StatInfo>.From(early);
        return context.Ok<global::app.type.item.path.@this.StatInfo>(context.FileSystem.Stat(this));
    }

    // --- Writes --------------------------------------------------------------

    public override async Task<data.@this<global::app.type.item.path.@this>> WriteText(string content, actor.context.@this context)
    {
        if (await AuthGate(Verb.Write, context) is { } early) return data.@this<global::app.type.item.path.@this>.From(early);
        await context.FileSystem.Write(this, Encoding.UTF8.GetBytes(content));
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
        var written = await new global::app.channel.type.file.@this(this, context)
            .Write(value ?? new data.@this("", context: context));
        return written.Success && !written.Exits
            ? context.Ok<global::app.type.item.path.@this>(this)
            : data.@this<global::app.type.item.path.@this>.From(written);
    }

    public override async Task<data.@this<global::app.type.item.path.@this>> WriteBytes(byte[] content, actor.context.@this context)
    {
        if (await AuthGate(Verb.Write, context) is { } early) return data.@this<global::app.type.item.path.@this>.From(early);
        await context.FileSystem.Write(this, content);
        return context.Ok<global::app.type.item.path.@this>(this);
    }

    public override async Task<data.@this<global::app.type.item.path.@this>> Append(string content, actor.context.@this context)
    {
        if (await AuthGate(Verb.Write, context) is { } early) return data.@this<global::app.type.item.path.@this>.From(early);
        await context.FileSystem.Append(this, content);
        return context.Ok<global::app.type.item.path.@this>(this);
    }

    public override async Task<data.@this<global::app.type.item.path.@this>> Mkdir(actor.context.@this context)
    {
        if (await AuthGate(Verb.Write, context) is { } early) return data.@this<global::app.type.item.path.@this>.From(early);
        context.FileSystem.Create(this);
        return context.Ok<global::app.type.item.path.@this>(this);
    }

    // --- Destructive ---------------------------------------------------------

    /// <summary>
    /// Delete with file-action options. Non-recursive directory deletes refuse
    /// non-empty directories with <c>DirectoryNotEmpty</c>; missing targets
    /// surface <c>NotFound</c> unless <paramref name="ignoreIfNotFound"/> is
    /// set. Returns the resulting Path (post-delete) wrapped in Data so the
    /// caller can read <see cref="Exists"/> on it.
    /// </summary>
    public override async Task<data.@this<global::app.type.item.path.@this>> Delete(global::app.type.item.@bool.@this recursive, global::app.type.item.@bool.@this ignoreIfNotFound, actor.context.@this context)
    {
        if (await AuthGate(Verb.Delete, context) is { } early) return data.@this<global::app.type.item.path.@this>.From(early);
        try
        {
            var files = context.FileSystem;
            if (files.IsFile(this))
                files.Delete(this, recursive: false);
            else if (files.IsFolder(this))
            {
                if (!recursive.Value && files.List(this, "*", recursive: false).Any())
                    return context.Error<global::app.type.item.path.@this>(new global::app.error.ServiceError(
                        $"Directory is not empty: {Raw}. Use recursive=true to delete contents.", "DirectoryNotEmpty", 400));
                files.Delete(this, recursive.Value);
            }
            else if (!ignoreIfNotFound.Value)
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
        return await BundledTransfer(fileDest, isMove: true, overwrite.Value, includeSubfolders: true, context);
    }

    /// <summary>
    /// Same-scheme copy with action-level options. See <see cref="MoveTo"/>.
    /// </summary>
    public override async Task<data.@this<global::app.type.item.path.@this>> CopyTo(global::app.type.item.path.@this destination, global::app.type.item.@bool.@this overwrite, global::app.type.item.@bool.@this includeSubfolders, actor.context.@this context)
    {
        if (destination is not @this fileDest) return await base.CopyTo(destination, overwrite, includeSubfolders, context);
        return await BundledTransfer(fileDest, isMove: false, overwrite.Value, includeSubfolders.Value, context);
    }

    /// <summary>
    /// Bundled-consent transfer. <paramref name="overwrite"/> and
    /// <paramref name="includeSubfolders"/> are threaded through PerformTransfer
    /// so action-handler options (file.copy / file.move) ride along on the
    /// same bundled-prompt flow.
    /// </summary>
    private async Task<data.@this<global::app.type.item.path.@this>> BundledTransfer(@this destination, bool isMove, bool overwrite, bool includeSubfolders, actor.context.@this context)
    {
        var sourceVerb = Verb.Read;
        var destVerb   = Verb.Write;

        var sourceAuth = await TryAuthorizeWithoutAsk(sourceVerb, context);
        var destAuth   = await destination.TryAuthorizeWithoutAsk(destVerb, context);

        bool sourceOk = sourceAuth?.Success == true;
        bool destOk   = destAuth?.Success == true;

        if (sourceOk && destOk)
            return await PerformTransfer(destination, isMove, overwrite, includeSubfolders, context);

        var question = new StringBuilder();
        question.Append(context.Actor!.Name).Append(" wants to:");
        if (!sourceOk) question.Append("\n  - read ").Append(Absolute);
        if (!destOk)   question.Append("\n  - write ").Append(destination.Absolute);
        question.Append("\n(y/n/a — covers all)");

        // the denial names the side not yet granted
        var request = !sourceOk ? BuildRequest(context.Actor!, sourceVerb) : BuildRequest(context.Actor!, destVerb);
        var consented = await context.Actor!.Permission.Ask(question.ToString(), request, context, async persist =>
        {
            if (!sourceOk) await StoreGrant(sourceVerb, persist, context);
            if (!destOk)   await destination.StoreGrant(destVerb, persist, context);
            return await PerformTransfer(destination, isMove, overwrite, includeSubfolders, context);
        });
        return data.@this<global::app.type.item.path.@this>.From(consented);
    }

    private async Task<data.@this?> TryAuthorizeWithoutAsk(Verb verb, actor.context.@this context)
    {
        if (IsInRoot(context)) return context.Ok();
        var existing = await context.Actor!.Permission.Find(this, verb);
        return existing != null ? context.Ok() : null;
    }

    private async Task StoreGrant(Verb verb, bool persist, actor.context.@this context)
    {
        var grant = BuildRequest(context.Actor!, verb);
        var d = new data.@this<global::app.type.item.permission.@this>("", grant, context: context);
        // Signing is at the I/O boundary now: a persisted grant is signed when it
        // crosses application/plang into the settings store. `persist` carries intent.
        await context.Actor!.Permission.Add(d, persist);
    }

    /// <summary>
    /// Performs the same-scheme transfer post-authorization. Handles both
    /// files and directories with action-handler options (overwrite, recursive
    /// subfolders) — absorbs <c>file/code/Default.cs::Default.Copy/Move</c>.
    /// Returns the new Path (post-transfer) wrapped in Data.
    /// </summary>
    private async Task<data.@this<global::app.type.item.path.@this>> PerformTransfer(@this destination, bool isMove, bool overwrite, bool includeSubfolders, actor.context.@this context)
    {
        var files = context.FileSystem;
        // A folder copied whole: its files, and its folders' too when includeSubfolders.
        async Task Copy(@this from, @this to)
        {
            files.Create(to);
            foreach (var entry in files.List(from, "*", recursive: false).ToList())
            {
                var into = new @this(PathHelper.Combine(to.Absolute, entry.FileName));
                if (files.IsFile(entry)) await files.Copy(entry, into, overwrite);
                else if (includeSubfolders) await Copy(entry, into);
            }
        }
        try
        {
            if (!files.IsFile(this) && !files.IsFolder(this))
                return context.Error<global::app.type.item.path.@this>(new global::app.error.ServiceError($"Not found: {Raw}", "NotFound", 404));

            // Directory transfer ------------------------------------------------
            if (files.IsFolder(this))
            {
                if (isMove) await files.Move(this, destination, overwrite);
                else await Copy(this, destination);
                return context.Ok<global::app.type.item.path.@this>(new @this(destination.Absolute) { Raw = destination.Raw });
            }

            // File transfer: into the destination when it names a folder ---------
            var target = files.IsFolder(destination) ? new @this(PathHelper.Combine(destination.Absolute, FileName)) : destination;
            if (isMove) await files.Move(this, target, overwrite);
            else await files.Copy(this, target, overwrite);

            // The destination as given — or, when it named a folder, the file under it.
            var destTyped = ReferenceEquals(target, destination) ? destination.Raw : destination.Combine(FileName).Raw;
            return context.Ok<global::app.type.item.path.@this>(new @this(target.Absolute) { Raw = destTyped });
        }
        catch (System.Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException)
        {
            return context.Error<global::app.type.item.path.@this>(new global::app.error.ServiceError(ex.Message, "IOError", 500));
        }
    }
}
