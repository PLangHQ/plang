using System.Text;
using app.types;
using app.errors;
using Verb = global::app.types.path.permission.verb.@this;
using ReadVerb = global::app.types.path.permission.verb.Read;
using WriteVerb = global::app.types.path.permission.verb.Write;
using DeleteVerb = global::app.types.path.permission.verb.Delete;

namespace app.types.path.file;

/// <summary>
/// FilePath verb implementations — relocated from the (now abstract) base.
/// Every method passes through <see cref="@this.AuthGate"/> (defined on the base)
/// before touching <c>System.IO</c>. Same-scheme MoveTo/CopyTo override the
/// base's naive default with <c>System.IO.File.Move</c>/<c>Copy</c> and the
/// bundled-consent prompt for fresh out-of-root pairs.
/// </summary>
public sealed partial class @this
{
    private void EnsureParentDir()
    {
        var dir = System.IO.Path.GetDirectoryName(Absolute);
        if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);
    }

    // --- Reads ---------------------------------------------------------------

    /// <summary>
    /// MIME-aware read. Authorize → (Builder snapshot for .pr) → bytes for
    /// binary MIME, text+TryConvertTo for the rest. The Data's <c>Type</c> is
    /// stamped from the file extension's MIME so downstream variable.set into a
    /// typed slot round-trips correctly. Replaces today's
    /// <c>file/code/Default.cs::Default.Read</c>.
    /// </summary>
    public override async Task<data.@this> ReadText()
    {
        if (await AuthGate(new Verb { Read = new ReadVerb() }) is { } early) return early;


        // During build: use snapshotted .pr content to avoid reading overwritten files.
        if (Context!.App.Builder.IsEnabled && Extension == ".pr")
        {
            var snapshot = Context!.App.Builder.GetPrSnapshot(Absolute);
            if (snapshot != null)
            {
                var snapshotType = data.type.FromMime(Context!.App.Formats.Mime(Extension));
                var snapshotClr = snapshotType.ClrType;
                if (snapshotClr != null && snapshotClr != typeof(string))
                {
                    var (converted, _) = global::app.types.@this.TryConvertTo(snapshot, snapshotClr);
                    if (converted != null)
                        return new data.@this(Raw, converted, snapshotType);
                }
                return new data.@this(Raw, snapshot, snapshotType);
            }
        }

        if (!System.IO.File.Exists(Absolute))
            return data.@this.FromError(new errors.ServiceError($"File not found: {Raw}", "NotFound", 404));

        try
        {
            var mime = Context!.App.Formats.Mime(Extension);
            var type = data.type.FromMime(mime);
            object content;

            if (type.ClrType == typeof(byte[]))
            {
                content = await System.IO.File.ReadAllBytesAsync(Absolute);
            }
            else
            {
                var text = await System.IO.File.ReadAllTextAsync(Absolute);

                if (Context!.App.Builder.IsEnabled && Extension == ".pr")
                    Context!.App.Builder.SnapshotPrFile(Absolute, text);

                var clr = type.ClrType;
                if (clr != null && clr != typeof(string))
                {
                    var (converted, _) = global::app.types.@this.TryConvertTo(text, clr);
                    content = converted ?? text;
                }
                else
                {
                    content = text;
                }
            }

            return new data.@this(Raw, content, data.type.FromMime(mime));
        }
        catch (System.Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException)
        {
            return data.@this.FromError(new errors.ServiceError(ex.Message, "IOError", 500));
        }
    }

    public override async Task<data.@this<byte[]>> ReadBytes()
    {
        if (await AuthGate(new Verb { Read = new ReadVerb() }) is { } early) return data.@this<byte[]>.From(early);
        return data.@this<byte[]>.Ok(await System.IO.File.ReadAllBytesAsync(Absolute));
    }

    public override async Task<data.@this<bool>> ExistsAsync()
    {
        if (await AuthGate(new Verb { Read = new ReadVerb() }) is { } early) return data.@this<bool>.From(early);
        return data.@this<bool>.Ok(System.IO.File.Exists(Absolute) || System.IO.Directory.Exists(Absolute));
    }

    /// <summary>
    /// Truthiness of a file path is "does it exist". Routes through the gated
    /// <see cref="ExistsAsync"/> — the same shape as <c>HttpPath.AsBooleanAsync</c>:
    /// a denied or errored probe answers false. Keeps the existence check behind
    /// <see cref="@this.AuthGate"/> so an out-of-root probe still needs a Read
    /// grant (in-root is free via IsInRoot). (codeanalyzer v2 N1)
    /// </summary>
    public override async Task<bool> AsBooleanAsync()
    {
        var existsResult = await ExistsAsync();
        return existsResult.Success && existsResult.Value is true;
    }

    /// <summary>
    /// List directory entries matching <paramref name="pattern"/>. Returns an
    /// array of FilePaths (Data&lt;Path[]&gt;), each Context-wired.
    /// </summary>
    public override async Task<data.@this<List<global::app.types.path.@this>>> List(string pattern, bool recursive)
    {
        if (await AuthGate(new Verb { Read = new ReadVerb() }) is { } early) return data.@this<List<global::app.types.path.@this>>.From(early);
        if (!System.IO.Directory.Exists(Absolute))
            return data.@this<List<global::app.types.path.@this>>.FromError(new errors.ServiceError($"Directory not found: {Raw}", "NotFound", 404));
        try
        {
            var option = recursive ? System.IO.SearchOption.AllDirectories : System.IO.SearchOption.TopDirectoryOnly;
            var files = System.IO.Directory.GetFiles(Absolute, pattern, option)
                .Select(f => (global::app.types.path.@this)new @this(f, Context))
                .ToList();
            return data.@this<List<global::app.types.path.@this>>.Ok(files);
        }
        catch (System.Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException)
        {
            return data.@this<List<global::app.types.path.@this>>.FromError(new errors.ServiceError(ex.Message, "IOError", 500));
        }
    }

    public override async Task<data.@this<global::app.types.path.@this.StatInfo>> Stat()
    {
        if (await AuthGate(new Verb { Read = new ReadVerb(Metadata: true) }) is { } early) return data.@this<global::app.types.path.@this.StatInfo>.From(early);
        if (System.IO.File.Exists(Absolute))
        {
            var info = new System.IO.FileInfo(Absolute);
            return data.@this<global::app.types.path.@this.StatInfo>.Ok(new StatInfo(Exists: true, IsFile: true, Length: info.Length, Modified: info.LastWriteTimeUtc));
        }
        if (System.IO.Directory.Exists(Absolute))
        {
            var info = new System.IO.DirectoryInfo(Absolute);
            return data.@this<global::app.types.path.@this.StatInfo>.Ok(new StatInfo(Exists: true, IsFile: false, Modified: info.LastWriteTimeUtc));
        }
        return data.@this<global::app.types.path.@this.StatInfo>.Ok(new StatInfo(Exists: false));
    }

    // --- Writes --------------------------------------------------------------

    public override async Task<data.@this<global::app.types.path.@this>> WriteText(string content)
    {
        if (await AuthGate(new Verb { Write = new WriteVerb() }) is { } early) return data.@this<global::app.types.path.@this>.From(early);
        EnsureParentDir();
        await System.IO.File.WriteAllTextAsync(Absolute, content);
        return data.@this<global::app.types.path.@this>.Ok(this);
    }

    /// <summary>
    /// File-save action target. <paramref name="value"/> may carry bytes,
    /// string, or an arbitrary object (serialized via the actor's
    /// extension-keyed Serializers). Returns the resulting Path wrapped in
    /// Data so the .pr's typed slot round-trips. Replaces today's
    /// <c>file/code/Default.cs::Default.Save</c>.
    /// </summary>
    public override async Task<data.@this<global::app.types.path.@this>> Save(data.@this? value)
    {
        if (await AuthGate(new Verb { Write = new WriteVerb() }) is { } early) return data.@this<global::app.types.path.@this>.From(early);

        try
        {
            EnsureParentDir();
            var raw = value?.Value;
            if (raw is byte[] bytes)
                await System.IO.File.WriteAllBytesAsync(Absolute, bytes);
            else if (raw is string str)
                await System.IO.File.WriteAllTextAsync(Absolute, str);
            else
            {
                await using var stream = System.IO.File.Create(Absolute);
                await Context!.Actor.Channels.Serializers.SerializeAsync(new global::app.channels.serializers.SerializeOptions
                    { Stream = stream, Data = raw, Extension = Extension });
            }
            return data.@this<global::app.types.path.@this>.Ok(this);
        }
        catch (System.Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException)
        {
            return data.@this<global::app.types.path.@this>.FromError(new errors.ServiceError(ex.Message, "IOError", 500));
        }
        catch (System.Exception ex) when (ex is System.Text.Json.JsonException or System.NotSupportedException)
        {
            return data.@this<global::app.types.path.@this>.FromError(new errors.ServiceError(ex.Message, "SerializationError", 500));
        }
    }

    public override async Task<data.@this<global::app.types.path.@this>> WriteBytes(byte[] content)
    {
        if (await AuthGate(new Verb { Write = new WriteVerb() }) is { } early) return data.@this<global::app.types.path.@this>.From(early);
        EnsureParentDir();
        await System.IO.File.WriteAllBytesAsync(Absolute, content);
        return data.@this<global::app.types.path.@this>.Ok(this);
    }

    public override async Task<data.@this<global::app.types.path.@this>> Append(string content)
    {
        if (await AuthGate(new Verb { Write = new WriteVerb() }) is { } early) return data.@this<global::app.types.path.@this>.From(early);
        EnsureParentDir();
        await System.IO.File.AppendAllTextAsync(Absolute, content);
        return data.@this<global::app.types.path.@this>.Ok(this);
    }

    public override async Task<data.@this<global::app.types.path.@this>> Mkdir()
    {
        if (await AuthGate(new Verb { Write = new WriteVerb() }) is { } early) return data.@this<global::app.types.path.@this>.From(early);
        System.IO.Directory.CreateDirectory(Absolute);
        return data.@this<global::app.types.path.@this>.Ok(this);
    }

    // --- Destructive ---------------------------------------------------------

    /// <summary>
    /// Delete with file-action options. Non-recursive directory deletes refuse
    /// non-empty directories with <c>DirectoryNotEmpty</c>; missing targets
    /// surface <c>NotFound</c> unless <paramref name="ignoreIfNotFound"/> is
    /// set. Returns the resulting Path (post-delete) wrapped in Data so the
    /// caller can read <see cref="Exists"/> on it.
    /// </summary>
    public override async Task<data.@this<global::app.types.path.@this>> Delete(bool recursive, bool ignoreIfNotFound)
    {
        if (await AuthGate(new Verb { Delete = new DeleteVerb() }) is { } early) return data.@this<global::app.types.path.@this>.From(early);
        try
        {
            if (System.IO.File.Exists(Absolute))
                System.IO.File.Delete(Absolute);
            else if (System.IO.Directory.Exists(Absolute))
            {
                if (!recursive && System.IO.Directory.GetFileSystemEntries(Absolute).Length > 0)
                    return data.@this<global::app.types.path.@this>.FromError(new errors.ServiceError(
                        $"Directory is not empty: {Raw}. Use recursive=true to delete contents.", "DirectoryNotEmpty", 400));
                System.IO.Directory.Delete(Absolute, recursive);
            }
            else if (!ignoreIfNotFound)
                return data.@this<global::app.types.path.@this>.FromError(new errors.ServiceError($"Not found: {Raw}", "NotFound", 404));

            return data.@this<global::app.types.path.@this>.Ok(this);
        }
        catch (System.Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException)
        {
            return data.@this<global::app.types.path.@this>.FromError(new errors.ServiceError(ex.Message, "IOError", 500));
        }
    }

    // --- Same-scheme fast paths for Move/Copy --------------------------------

    /// <summary>
    /// Same-scheme move with action-level options. Bundled-consent for the
    /// out-of-root pair stays — calls into BundledTransfer with the overwrite
    /// option threaded through PerformTransfer. Cross-scheme moves fall
    /// through to the base default (ReadBytes → WriteBytes → Delete).
    /// </summary>
    public override async Task<data.@this<global::app.types.path.@this>> MoveTo(global::app.types.path.@this destination, bool overwrite)
    {
        if (destination is not @this fileDest) return await base.MoveTo(destination, overwrite);
        return await BundledTransfer(fileDest, isMove: true, overwrite: overwrite, includeSubfolders: true);
    }

    /// <summary>
    /// Same-scheme copy with action-level options. See <see cref="MoveTo(global::app.types.path.@this, bool)"/>.
    /// </summary>
    public override async Task<data.@this<global::app.types.path.@this>> CopyTo(global::app.types.path.@this destination, bool overwrite, bool includeSubfolders)
    {
        if (destination is not @this fileDest) return await base.CopyTo(destination, overwrite, includeSubfolders);
        return await BundledTransfer(fileDest, isMove: false, overwrite: overwrite, includeSubfolders: includeSubfolders);
    }

    private static string ResolveDestinationPath(@this source, @this destination)
    {
        if (System.IO.File.Exists(source.Absolute) && System.IO.Directory.Exists(destination.Absolute))
            return System.IO.Path.Combine(destination.Absolute, source.FileName);
        return destination.Absolute;
    }

    private static void CopyDirectory(string src, string dest, bool overwrite, bool includeSubfolders)
    {
        System.IO.Directory.CreateDirectory(dest);
        foreach (var file in System.IO.Directory.GetFiles(src))
        {
            var fileName = System.IO.Path.GetFileName(file);
            System.IO.File.Copy(file, System.IO.Path.Combine(dest, fileName), overwrite);
        }
        if (!includeSubfolders) return;
        foreach (var subDir in System.IO.Directory.GetDirectories(src))
        {
            var dirName = System.IO.Path.GetFileName(subDir);
            CopyDirectory(subDir, System.IO.Path.Combine(dest, dirName), overwrite, includeSubfolders);
        }
    }

    /// <summary>
    /// Bundled-consent transfer. <paramref name="overwrite"/> and
    /// <paramref name="includeSubfolders"/> are threaded through PerformTransfer
    /// so action-handler options (file.copy / file.move) ride along on the
    /// same bundled-prompt flow. Backward-compatible default
    /// (overwrite=true, includeSubfolders=true) matches the prior
    /// permission-only behavior.
    /// </summary>
    private async Task<data.@this<global::app.types.path.@this>> BundledTransfer(@this destination, bool isMove, bool overwrite = true, bool includeSubfolders = true)
    {
        var sourceVerb = new Verb { Read = new ReadVerb() };
        var destVerb   = new Verb { Write = new WriteVerb() };

        var sourceAuth = await TryAuthorizeWithoutAsk(sourceVerb);
        var destAuth   = await destination.TryAuthorizeWithoutAsk(destVerb);

        bool sourceOk = sourceAuth?.Success == true;
        bool destOk   = destAuth?.Success == true;

        if (sourceOk && destOk)
            return await PerformTransfer(destination, isMove, overwrite, includeSubfolders);

        string prefix = "";
        while (true)
        {
            var sb = new StringBuilder();
            sb.Append(prefix);
            sb.Append(Context!.Actor!.Name).Append(" wants to:");
            if (!sourceOk) sb.Append("\n  - read ").Append(Absolute);
            if (!destOk)   sb.Append("\n  - write ").Append(destination.Absolute);
            sb.Append("\n(y/n/a — covers all)");

            var askAction = new modules.output.ask
            {
                Context = Context,
                Question = new data.@this<string>("", sb.ToString()),
            };
            var askResult = await Context!.App.RunAction(askAction, Context);

            if (askResult.Type?.ClrType.Exit() == true) return data.@this<global::app.types.path.@this>.From(askResult);
            if (!askResult.Success) return data.@this<global::app.types.path.@this>.From(askResult);

            var answer = askResult.Value?.ToString()?.Trim();
            switch (answer)
            {
                case "a":
                    if (!sourceOk) await StoreGrant(sourceVerb, persist: true);
                    if (!destOk)   await destination.StoreGrant(destVerb, persist: true);
                    return await PerformTransfer(destination, isMove, overwrite, includeSubfolders);
                case "y":
                    if (!sourceOk) await StoreGrant(sourceVerb, persist: false);
                    if (!destOk)   await destination.StoreGrant(destVerb, persist: false);
                    return await PerformTransfer(destination, isMove, overwrite, includeSubfolders);
                case "n":
                    var denied = !sourceOk
                        ? new global::app.errors.PermissionDenied(BuildRequest(Context!.Actor!, sourceVerb))
                        : new global::app.errors.PermissionDenied(BuildRequest(Context!.Actor!, destVerb));
                    return data.@this<global::app.types.path.@this>.FromError(denied);
                default:
                    prefix = $"Invalid answer '{answer}'. ";
                    continue;
            }
        }
    }

    private async Task<data.@this?> TryAuthorizeWithoutAsk(Verb verb)
    {
        if (IsInRoot()) return data.@this.Ok();
        var existing = await Context!.Actor!.Permission.Find(this, verb);
        return existing != null ? data.@this.Ok() : null;
    }

    private async Task StoreGrant(Verb verb, bool persist)
    {
        var permission = BuildRequest(Context!.Actor!, verb);
        var d = new data.@this<global::app.types.path.permission.@this>("", permission) { Context = Context };
        if (persist) d.EnsureSigned();
        await Context!.Actor!.Permission.Add(d);
    }

    /// <summary>
    /// Performs the same-scheme transfer post-authorization. Handles both
    /// files and directories with action-handler options (overwrite, recursive
    /// subfolders) — absorbs <c>file/code/Default.cs::Default.Copy/Move</c>.
    /// Returns the new Path (post-transfer) wrapped in Data, with Source set
    /// to the original Absolute so action-handler consumers can read it.
    /// </summary>
    private Task<data.@this<global::app.types.path.@this>> PerformTransfer(@this destination, bool isMove, bool overwrite, bool includeSubfolders)
    {
        try
        {
            if (!System.IO.File.Exists(Absolute) && !System.IO.Directory.Exists(Absolute))
                return Task.FromResult(data.@this<global::app.types.path.@this>.FromError(new errors.ServiceError($"Not found: {Raw}", "NotFound", 404)));

            // Directory transfer ------------------------------------------------
            if (System.IO.Directory.Exists(Absolute))
            {
                var destDir0 = System.IO.Path.GetDirectoryName(destination.Absolute);
                if (!string.IsNullOrEmpty(destDir0) && !System.IO.Directory.Exists(destDir0))
                    System.IO.Directory.CreateDirectory(destDir0);

                if (isMove)
                {
                    if (overwrite && System.IO.Directory.Exists(destination.Absolute))
                        System.IO.Directory.Delete(destination.Absolute, recursive: true);
                    System.IO.Directory.Move(Absolute, destination.Absolute);
                    return Task.FromResult(
                        data.@this<global::app.types.path.@this>.Ok(new @this(destination.Absolute, Context, source: Absolute)));
                }

                CopyDirectory(Absolute, destination.Absolute, overwrite, includeSubfolders);
                return Task.FromResult(
                    data.@this<global::app.types.path.@this>.Ok(new @this(destination.Absolute, Context, source: Absolute)));
            }

            // File transfer -----------------------------------------------------
            var destPath = ResolveDestinationPath(this, destination);
            var destDir = System.IO.Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(destDir) && !System.IO.Directory.Exists(destDir))
                System.IO.Directory.CreateDirectory(destDir);

            if (isMove) System.IO.File.Move(Absolute, destPath, overwrite);
            else        System.IO.File.Copy(Absolute, destPath, overwrite);

            return Task.FromResult(
                data.@this<global::app.types.path.@this>.Ok(new @this(destPath, Context, source: Absolute)));
        }
        catch (System.Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException)
        {
            return Task.FromResult(data.@this<global::app.types.path.@this>.FromError(new errors.ServiceError(ex.Message, "IOError", 500)));
        }
    }
}
