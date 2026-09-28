using System.Text;
using app.type;
using app.error;
using app.Utils;
using app.data;
using Verb = global::app.type.item.permission.Verb;

namespace app.type.item.path.file;

/// <summary>
/// FilePath verb implementations — relocated from the (now abstract) base.
/// Every method passes through <see cref="@this.AuthGate"/> (defined on the base)
/// before touching <c>System.IO</c>. Same-scheme MoveTo/CopyTo override the
/// base's naive default with <c>System.IO.File.Move</c>/<c>Copy</c> and the
/// bundled-consent prompt for fresh out-of-root pairs. Every verb acts as the
/// caller: its context carries the actor asked for permission and is the context
/// the result Data is born with.
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
        if (!System.IO.File.Exists(Absolute))
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

    private void EnsureParentDir()
    {
        var dir = PathHelper.GetDirectoryName(Absolute);
        if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);
    }

    // --- Reads ---------------------------------------------------------------

    /// <summary>A file lands as a <c>file</c> reference, a folder as a <c>directory</c>; nothing there is
    /// NotFound (404) now, at the read, not at first touch. The stat tells which.</summary>
    public override async Task<data.@this> Read(actor.context.@this context, data.@this<global::app.type.item.@bool.@this>? template = null)
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
        var marked = template != null && await template.ToBooleanAsync() ? "plang" : null;
        return await context.App.type.list[new global::app.type.@this("file", (string?)null, template: marked), context].Create(this, context, FileName);
    }

    /// <summary>The <c>file</c> reference's type; a location with no known format expects nothing. A file
    /// missing now (or one the build may not stat) is a warning on the build's "builder" channel, never a
    /// failure — the read at run asks again under its own grant.</summary>
    public override async Task<data.@this> Expect(actor.context.@this context)
    {
        if (string.IsNullOrEmpty(Extension) || MimeType(context) == "application/octet-stream") return context.Ok();
        var exists = await ExistsAsync(context);
        string? message = !exists.Success
            ? $"could not check '{this}': {exists.Error?.Message} ({exists.Error?.Key})"
            : !await exists.ToBooleanAsync() ? $"'{this}' does not exist on disk" : null;
        if (message != null && context.Actor.Channel.Get("builder") is { } builder)
            await builder.WriteAsync(context.Ok(new global::app.type.item.dict.@this().Set("message", message)));
        return context.Ok(context.App.type.list[new global::app.type.item.file.@this(this, context).Type, context]);
    }

    internal override async Task<data.@this<global::app.type.item.binary.@this>> Bytes(actor.context.@this context)
    {
        if (await AuthGate(Verb.Read, context) is { } early) return data.@this<global::app.type.item.binary.@this>.From(early);
        if (!System.IO.File.Exists(Absolute))
            return context.Error<global::app.type.item.binary.@this>(new global::app.error.ServiceError($"File not found: {Raw}", "NotFound", 404));
        try
        {
            return context.Ok<global::app.type.item.binary.@this>(new global::app.type.item.binary.@this(await System.IO.File.ReadAllBytesAsync(Absolute)));
        }
        catch (System.Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException)
        {
            return context.Error<global::app.type.item.binary.@this>(new global::app.error.ServiceError(ex.Message, "IOError", 500));
        }
    }

    public override async Task<data.@this<global::app.type.item.@bool.@this>> ExistsAsync(actor.context.@this context)
    {
        if (await AuthGate(Verb.Read, context) is { } early) return data.@this<global::app.type.item.@bool.@this>.From(early);
        return context.Ok<global::app.type.item.@bool.@this>(System.IO.File.Exists(Absolute) || System.IO.Directory.Exists(Absolute));
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
        if (!System.IO.Directory.Exists(Absolute))
            return context.Error<global::app.type.item.list.@this<global::app.type.item.path.@this>>(new global::app.error.ServiceError($"Directory not found: {Raw}", "NotFound", 404));
        try
        {
            var option = recursive.Value ? System.IO.SearchOption.AllDirectories : System.IO.SearchOption.TopDirectoryOnly;
            // Each entry is this folder combined with its place under it — typed text included.
            var files = System.IO.Directory.GetFiles(Absolute, pattern.ToString(), option)
                .Select(f => new data.@this("", Combine(f[Absolute.Length..].TrimStart(PathHelper.DirectorySeparatorChar, PathHelper.AltDirectorySeparatorChar)),
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
        if (System.IO.File.Exists(Absolute))
        {
            var info = new System.IO.FileInfo(Absolute);
            return context.Ok<global::app.type.item.path.@this.StatInfo>(new StatInfo(Exists: true, IsFile: true, Length: info.Length, Modified: info.LastWriteTimeUtc));
        }
        if (System.IO.Directory.Exists(Absolute))
        {
            var info = new System.IO.DirectoryInfo(Absolute);
            return context.Ok<global::app.type.item.path.@this.StatInfo>(new StatInfo(Exists: true, IsFile: false, Modified: info.LastWriteTimeUtc));
        }
        return context.Ok<global::app.type.item.path.@this.StatInfo>(new StatInfo(Exists: false));
    }

    // --- Writes --------------------------------------------------------------

    public override async Task<data.@this<global::app.type.item.path.@this>> WriteText(string content, actor.context.@this context)
    {
        if (await AuthGate(Verb.Write, context) is { } early) return data.@this<global::app.type.item.path.@this>.From(early);
        EnsureParentDir();
        await System.IO.File.WriteAllTextAsync(Absolute, content);
        return context.Ok<global::app.type.item.path.@this>(this);
    }

    /// <summary>
    /// The write door, the file reference's value door mirrored: the file's extension is a format, and the format
    /// writes the value (<c>.pr</c> → goal's, <c>.json</c> → json). A caller says where, never how. Returns
    /// the resulting Path wrapped in Data so the .pr's typed slot round-trips.
    /// </summary>
    public override async Task<data.@this<global::app.type.item.path.@this>> Save(data.@this? value, actor.context.@this context)
    {
        if (await AuthGate(Verb.Write, context) is { } early) return data.@this<global::app.type.item.path.@this>.From(early);

        try
        {
            EnsureParentDir();
            var raw = value == null ? null : await value.Value();
            // Raw bytes ride straight to disk — a binary value IS its byte form, and the text
            // writer would base64 it (bytes are bytes at the System.IO edge, no serializer).
            if (raw is global::app.type.item.binary.@this binv)
                await System.IO.File.WriteAllBytesAsync(Absolute, binv.Value);
            else
            {
                // The file's extension is the format that writes the value (a goal to .pr its program
                // form, a text value to .json json-quoted, to .txt bare). A format that doesn't write this
                // value (an unknown extension, one of binary's) falls to text — a leaf bare, a container as
                // its json content: this door's own rule for a file of no known format.
                using var encoded = new System.IO.MemoryStream();
                var result = await Kind(context).kind.Encode(encoded, value!, context);
                if (!result.Success && result.Error?.Key == "NoEncoder")
                {
                    encoded.SetLength(0);
                    result = await context.App.type.list["text"].kind.Encode(encoded, value!, context);
                }
                if (!result.Success)
                    return context.Error<global::app.type.item.path.@this>(result.Error!);
                await System.IO.File.WriteAllBytesAsync(Absolute, encoded.ToArray());
            }
            return context.Ok<global::app.type.item.path.@this>(this);
        }
        catch (System.Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException)
        {
            return context.Error<global::app.type.item.path.@this>(new global::app.error.ServiceError(ex.Message, "IOError", 500));
        }
        catch (System.Exception ex) when (ex is System.Text.Json.JsonException or System.NotSupportedException)
        {
            return context.Error<global::app.type.item.path.@this>(new global::app.error.ServiceError(ex.Message, "SerializationError", 500));
        }
    }

    public override async Task<data.@this<global::app.type.item.path.@this>> WriteBytes(byte[] content, actor.context.@this context)
    {
        if (await AuthGate(Verb.Write, context) is { } early) return data.@this<global::app.type.item.path.@this>.From(early);
        EnsureParentDir();
        await System.IO.File.WriteAllBytesAsync(Absolute, content);
        return context.Ok<global::app.type.item.path.@this>(this);
    }

    public override async Task<data.@this<global::app.type.item.path.@this>> Append(string content, actor.context.@this context)
    {
        if (await AuthGate(Verb.Write, context) is { } early) return data.@this<global::app.type.item.path.@this>.From(early);
        EnsureParentDir();
        await System.IO.File.AppendAllTextAsync(Absolute, content);
        return context.Ok<global::app.type.item.path.@this>(this);
    }

    public override async Task<data.@this<global::app.type.item.path.@this>> Mkdir(actor.context.@this context)
    {
        if (await AuthGate(Verb.Write, context) is { } early) return data.@this<global::app.type.item.path.@this>.From(early);
        System.IO.Directory.CreateDirectory(Absolute);
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
            if (System.IO.File.Exists(Absolute))
                System.IO.File.Delete(Absolute);
            else if (System.IO.Directory.Exists(Absolute))
            {
                if (!recursive.Value && System.IO.Directory.GetFileSystemEntries(Absolute).Length > 0)
                    return context.Error<global::app.type.item.path.@this>(new global::app.error.ServiceError(
                        $"Directory is not empty: {Raw}. Use recursive=true to delete contents.", "DirectoryNotEmpty", 400));
                System.IO.Directory.Delete(Absolute, recursive.Value);
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

    private static string ResolveDestinationPath(@this source, @this destination)
    {
        if (System.IO.File.Exists(source.Absolute) && System.IO.Directory.Exists(destination.Absolute))
            return PathHelper.Combine(destination.Absolute, source.FileName);
        return destination.Absolute;
    }

    private static void CopyDirectory(string src, string dest, bool overwrite, bool includeSubfolders)
    {
        System.IO.Directory.CreateDirectory(dest);
        foreach (var file in System.IO.Directory.GetFiles(src))
        {
            var fileName = PathHelper.GetFileName(file);
            System.IO.File.Copy(file, PathHelper.Combine(dest, fileName), overwrite);
        }
        if (!includeSubfolders) return;
        foreach (var subDir in System.IO.Directory.GetDirectories(src))
        {
            var dirName = PathHelper.GetFileName(subDir);
            CopyDirectory(subDir, PathHelper.Combine(dest, dirName), overwrite, includeSubfolders);
        }
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
            return PerformTransfer(destination, isMove, overwrite, includeSubfolders, context);

        string prefix = "";
        while (true)
        {
            var sb = new StringBuilder();
            sb.Append(prefix);
            sb.Append(context.Actor!.Name).Append(" wants to:");
            if (!sourceOk) sb.Append("\n  - read ").Append(Absolute);
            if (!destOk)   sb.Append("\n  - write ").Append(destination.Absolute);
            sb.Append("\n(y/n/a — covers all)");

            var askAction = new module.action.output.ask(context)
            {
                Question = new data.@this<global::app.type.item.text.@this>("", sb.ToString(), context: context),
            };
            var askResult = await context.App.Run(askAction, context);

            if (askResult.ShouldExit()) return data.@this<global::app.type.item.path.@this>.From(askResult);
            if (!askResult.Success) return data.@this<global::app.type.item.path.@this>.From(askResult);

            var ask = await askResult.Value() as global::app.module.action.output.Ask;
            var answer = ask?.Answer?.Trim();
            switch (answer)
            {
                case "a":
                    if (!sourceOk) await StoreGrant(sourceVerb, persist: true, context);
                    if (!destOk)   await destination.StoreGrant(destVerb, persist: true, context);
                    return PerformTransfer(destination, isMove, overwrite, includeSubfolders, context);
                case "y":
                    if (!sourceOk) await StoreGrant(sourceVerb, persist: false, context);
                    if (!destOk)   await destination.StoreGrant(destVerb, persist: false, context);
                    return PerformTransfer(destination, isMove, overwrite, includeSubfolders, context);
                case "n":
                    var denied = !sourceOk
                        ? new global::app.error.PermissionDenied(BuildRequest(context.Actor!, sourceVerb))
                        : new global::app.error.PermissionDenied(BuildRequest(context.Actor!, destVerb));
                    return context.Error<global::app.type.item.path.@this>(denied);
                default:
                    prefix = $"Invalid answer '{answer}'. ";
                    continue;
            }
        }
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
    private data.@this<global::app.type.item.path.@this> PerformTransfer(@this destination, bool isMove, bool overwrite, bool includeSubfolders, actor.context.@this context)
    {
        try
        {
            if (!System.IO.File.Exists(Absolute) && !System.IO.Directory.Exists(Absolute))
                return context.Error<global::app.type.item.path.@this>(new global::app.error.ServiceError($"Not found: {Raw}", "NotFound", 404));

            // Directory transfer ------------------------------------------------
            if (System.IO.Directory.Exists(Absolute))
            {
                var destDir0 = PathHelper.GetDirectoryName(destination.Absolute);
                if (!string.IsNullOrEmpty(destDir0) && !System.IO.Directory.Exists(destDir0))
                    System.IO.Directory.CreateDirectory(destDir0);

                if (isMove)
                {
                    if (overwrite && System.IO.Directory.Exists(destination.Absolute))
                        System.IO.Directory.Delete(destination.Absolute, recursive: true);
                    System.IO.Directory.Move(Absolute, destination.Absolute);
                    return context.Ok<global::app.type.item.path.@this>(new @this(destination.Absolute) { Raw = destination.Raw });
                }

                CopyDirectory(Absolute, destination.Absolute, overwrite, includeSubfolders);
                return context.Ok<global::app.type.item.path.@this>(new @this(destination.Absolute) { Raw = destination.Raw });
            }

            // File transfer -----------------------------------------------------
            var destPath = ResolveDestinationPath(this, destination);
            var destDir = PathHelper.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(destDir) && !System.IO.Directory.Exists(destDir))
                System.IO.Directory.CreateDirectory(destDir);

            if (isMove) System.IO.File.Move(Absolute, destPath, overwrite);
            else        System.IO.File.Copy(Absolute, destPath, overwrite);

            // The destination as given — or, when it named a folder, the file under it.
            var destTyped = destPath == destination.Absolute ? destination.Raw : destination.Combine(FileName).Raw;
            return context.Ok<global::app.type.item.path.@this>(new @this(destPath) { Raw = destTyped });
        }
        catch (System.Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException)
        {
            return context.Error<global::app.type.item.path.@this>(new global::app.error.ServiceError(ex.Message, "IOError", 500));
        }
    }
}
