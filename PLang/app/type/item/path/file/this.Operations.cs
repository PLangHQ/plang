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

    /// <summary>
    /// MIME-aware read. Authorize → (Builder snapshot for .pr) → bytes for
    /// binary MIME, text+TryConvert for the rest. The Data's <c>Type</c> is
    /// stamped from the file extension's MIME so downstream variable.set into a
    /// typed slot round-trips correctly. Replaces today's
    /// <c>file/code/Default.cs::Default.Read</c>.
    /// </summary>
    public override async Task<data.@this> ReadText(actor.context.@this context)
    {
        if (await AuthGate(Verb.Read, context) is { } early) return early;

        // The declared {type, kind} the extension's mime stamps ({goal} for .pr,
        // {object, json} for .json, {text} for .txt) — the SAME derivation build-time
        // file.read.Build() uses, so build and runtime agree. Only the type is stamped
        // here; materialization is deferred (see below).
        var mime = context.App.Format.Mime(Extension);
        var type = context.App.Type.Mime(mime);

        // During build: a .pr may be mid-rewrite on disk — read the snapshotted bytes.
        // Still deferred: the source holds the raw form under {goal}; .Value() runs the reader.
        // TODO(build-mode-inversion): build mode sniffed from a foreign layer (the file op
        // shouldn't know build mode exists) — invert to a build-born .pr read decorator (plan §6.D).
        if (context.App.Mode.Value == global::app.Mode.Build && Extension == ".pr")
        {
            var snapshot = context.App.Build!.GetPrSnapshot(Absolute);
            if (snapshot != null)
                return new global::app.data.@this(Raw, type.Create(snapshot, context), context: context);
        }

        if (!System.IO.File.Exists(Absolute))
            return context.Error(new global::app.error.ServiceError($"File not found: {Raw}", "NotFound", 404));

        try
        {
            var bytes = await System.IO.File.ReadAllBytesAsync(Absolute);

            // Record the .pr in the build snapshot cache so a later read this build sees
            // the pre-overwrite content. Perimeter decode — a string only appears here.
            // TODO(build-mode-inversion): foreign-layer build sniff — invert (plan §6.D).
            if (context.App.Mode.Value == global::app.Mode.Build && Extension == ".pr")
                context.App.Build!.SnapshotPrFile(Absolute, System.Text.Encoding.UTF8.GetString(bytes));

            // Deferred: the source holds the raw bytes under their declared {type, kind};
            // the parse runs through the ONE reader on first touch (.Value()) — a .pr → the
            // goal reader → clr<goal>, a .json → the json reader → clr(json). No eager convert.
            return new global::app.data.@this(Raw, type.Create(bytes, context), context: context);
        }
        catch (System.Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException)
        {
            return context.Error(new global::app.error.ServiceError(ex.Message, "IOError", 500));
        }
    }

    public override async Task<data.@this<global::app.type.item.binary.@this>> ReadBytes(actor.context.@this context)
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
    public override async Task<data.@this<global::app.type.item.list.@this<global::app.type.item.path.@this>>> List(string pattern, bool recursive, actor.context.@this context)
    {
        if (await AuthGate(Verb.Read, context) is { } early) return data.@this<global::app.type.item.list.@this<global::app.type.item.path.@this>>.From(early);
        if (!System.IO.Directory.Exists(Absolute))
            return context.Error<global::app.type.item.list.@this<global::app.type.item.path.@this>>(new global::app.error.ServiceError($"Directory not found: {Raw}", "NotFound", 404));
        try
        {
            var option = recursive ? System.IO.SearchOption.AllDirectories : System.IO.SearchOption.TopDirectoryOnly;
            // Each entry is this folder combined with its place under it — typed text included.
            var files = System.IO.Directory.GetFiles(Absolute, pattern, option)
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
    /// File-save action target. <paramref name="value"/> may carry bytes,
    /// string, or an arbitrary object (serialized via the actor's
    /// extension-keyed Serializers). Returns the resulting Path wrapped in
    /// Data so the .pr's typed slot round-trips. Replaces today's
    /// <c>file/code/Default.cs::Default.Save</c>.
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
                await using var stream = System.IO.File.Create(Absolute);
                // file-save owns its selector (its Extension); the VALUE writes itself into that
                // format's writer: a registered extension writes that format (a text value to
                // .json is json-quoted, to .txt is bare); an unregistered one falls to Text —
                // a leaf bare, a container as its json content. No special text arm, no shape branch.
                var serializers = context.Actor.Channel.Serializers;
                var serializer = serializers.GetByExtension(Extension) ?? serializers.Text;
                var serResult = await serializer.SerializeAsync(stream, value!);
                if (!serResult.Success)
                    return context.Error<global::app.type.item.path.@this>(serResult.Error!);
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
    public override async Task<data.@this<global::app.type.item.path.@this>> Delete(bool recursive, bool ignoreIfNotFound, actor.context.@this context)
    {
        if (await AuthGate(Verb.Delete, context) is { } early) return data.@this<global::app.type.item.path.@this>.From(early);
        try
        {
            if (System.IO.File.Exists(Absolute))
                System.IO.File.Delete(Absolute);
            else if (System.IO.Directory.Exists(Absolute))
            {
                if (!recursive && System.IO.Directory.GetFileSystemEntries(Absolute).Length > 0)
                    return context.Error<global::app.type.item.path.@this>(new global::app.error.ServiceError(
                        $"Directory is not empty: {Raw}. Use recursive=true to delete contents.", "DirectoryNotEmpty", 400));
                System.IO.Directory.Delete(Absolute, recursive);
            }
            else if (!ignoreIfNotFound)
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
    /// through to the base default (ReadBytes → WriteBytes → Delete).
    /// </summary>
    public override async Task<data.@this<global::app.type.item.path.@this>> MoveTo(global::app.type.item.path.@this destination, bool overwrite, actor.context.@this context)
    {
        if (destination is not @this fileDest) return await base.MoveTo(destination, overwrite, context);
        return await BundledTransfer(fileDest, isMove: true, overwrite, includeSubfolders: true, context);
    }

    /// <summary>
    /// Same-scheme copy with action-level options. See <see cref="MoveTo"/>.
    /// </summary>
    public override async Task<data.@this<global::app.type.item.path.@this>> CopyTo(global::app.type.item.path.@this destination, bool overwrite, bool includeSubfolders, actor.context.@this context)
    {
        if (destination is not @this fileDest) return await base.CopyTo(destination, overwrite, includeSubfolders, context);
        return await BundledTransfer(fileDest, isMove: false, overwrite, includeSubfolders, context);
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
