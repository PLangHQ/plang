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
/// bundled-consent prompt for fresh out-of-root pairs.
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
    public override async Task<data.@this> LoadAssemblyAsync()
    {
        if (await AuthGate(Verb.Execute) is { } early)
            return early;
        try
        {
            var asm = System.Reflection.Assembly.LoadFrom(Absolute);
            return Context.Ok((object)asm);
        }
        catch (System.Exception ex) when (ex is System.IO.FileNotFoundException or System.IO.FileLoadException or System.BadImageFormatException)
        {
            return Context.Error(new global::app.error.ServiceError($"Failed to load assembly: {ex.Message}", "AssemblyLoadFailed", 500));
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
    public override async Task<data.@this> ReadText()
    {
        if (await AuthGate(Verb.Read) is { } early) return early;

        // The declared {type, kind} the extension's mime stamps ({goal} for .pr,
        // {object, json} for .json, {text} for .txt) — the SAME derivation build-time
        // file.read.Build() uses, so build and runtime agree. Only the type is stamped
        // here; materialization is deferred (see below).
        var mime = Context.App.Format.Mime(Extension);
        var type = Context.App.Format.TypeFromMime(mime);
        type.Context = Context;

        // The serializer whose encoding these bytes are in — a registered mime reads
        // structured via its own reader; an unregistered one (a .pr, an image) reads
        // through the value reader (the text serializer). The SAME selection the channel
        // boundary makes in Channel.StampValue, so file and channel reads agree on one reader.
        var serializers = Context.Actor?.Channel.Serializers;
        var serializer = serializers?.GetByType(mime) ?? serializers?.Text;
        var format = serializer?.Type ?? "application/plang";

        // During build: a .pr may be mid-rewrite on disk — read the snapshotted bytes.
        // Still deferred: the source holds the raw form under {goal}; .Value() runs the reader.
        // TODO(build-mode-inversion): build mode sniffed from a foreign layer (the file op
        // shouldn't know build mode exists) — invert to a build-born .pr read decorator (plan §6.D).
        var build = Context.App.Build;
        if (build != null && Extension == ".pr")
        {
            var snapshot = build.GetPrSnapshot(Absolute);
            if (snapshot != null)
                return global::app.data.@this.FromRaw(snapshot, type, Context, Raw, format);
        }

        if (!System.IO.File.Exists(Absolute))
            return Context.Error(new global::app.error.ServiceError($"File not found: {Raw}", "NotFound", 404));

        try
        {
            var bytes = await System.IO.File.ReadAllBytesAsync(Absolute);

            // Record the .pr in the build snapshot cache so a later read this build sees
            // the pre-overwrite content. Perimeter decode — a string only appears here.
            // TODO(build-mode-inversion): foreign-layer build sniff — invert (plan §6.D).
            if (build != null && Extension == ".pr")
                build.SnapshotPrFile(Absolute, System.Text.Encoding.UTF8.GetString(bytes));

            // Deferred: the source holds the raw bytes under their declared {type, kind};
            // the parse runs through the ONE reader on first touch (.Value()) — a .pr → the
            // goal reader → clr<goal>, a .json → the json reader → clr(json). No eager convert.
            return global::app.data.@this.FromRaw(bytes, type, Context, Raw, format);
        }
        catch (System.Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException)
        {
            return Context.Error(new global::app.error.ServiceError(ex.Message, "IOError", 500));
        }
    }

    public override async Task<data.@this<global::app.type.item.binary.@this>> ReadBytes()
    {
        if (await AuthGate(Verb.Read) is { } early) return data.@this<global::app.type.item.binary.@this>.From(early);
        if (!System.IO.File.Exists(Absolute))
            return Context.Error<global::app.type.item.binary.@this>(new global::app.error.ServiceError($"File not found: {Raw}", "NotFound", 404));
        try
        {
            return Context.Ok<global::app.type.item.binary.@this>(new global::app.type.item.binary.@this(await System.IO.File.ReadAllBytesAsync(Absolute)));
        }
        catch (System.Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException)
        {
            return Context.Error<global::app.type.item.binary.@this>(new global::app.error.ServiceError(ex.Message, "IOError", 500));
        }
    }

    public override async Task<data.@this<global::app.type.item.@bool.@this>> ExistsAsync()
    {
        if (await AuthGate(Verb.Read) is { } early) return data.@this<global::app.type.item.@bool.@this>.From(early);
        return Context.Ok<global::app.type.item.@bool.@this>(System.IO.File.Exists(Absolute) || System.IO.Directory.Exists(Absolute));
    }

    /// <summary>
    /// Truthiness of a file path is "does it exist". Routes through the gated
    /// <see cref="ExistsAsync"/> — the same shape as <c>HttpPath.AsBooleanAsync</c>:
    /// a denied or errored probe answers false. Keeps the existence check behind
    /// <see cref="@this.AuthGate"/> so an out-of-root probe still needs a Read
    /// grant (in-root is free via IsInRoot).
    /// </summary>
    public override async Task<bool> AsBooleanAsync()
    {
        var existsResult = await ExistsAsync();
        return existsResult.Success && await existsResult.ToBooleanAsync();
    }

    /// <summary>
    /// List directory entries matching <paramref name="pattern"/>. Returns an
    /// array of FilePaths (Data&lt;Path[]&gt;), each Context-wired.
    /// </summary>
    public override async Task<data.@this<global::app.type.list.@this<global::app.type.item.path.@this>>> List(string pattern, bool recursive)
    {
        if (await AuthGate(Verb.Read) is { } early) return data.@this<global::app.type.list.@this<global::app.type.item.path.@this>>.From(early);
        if (!System.IO.Directory.Exists(Absolute))
            return Context.Error<global::app.type.list.@this<global::app.type.item.path.@this>>(new global::app.error.ServiceError($"Directory not found: {Raw}", "NotFound", 404));
        try
        {
            var option = recursive ? System.IO.SearchOption.AllDirectories : System.IO.SearchOption.TopDirectoryOnly;
            var files = System.IO.Directory.GetFiles(Absolute, pattern, option)
                .Select(f => new data.@this("", (global::app.type.item.path.@this)new @this(f, Context), context: Context))
                .ToList();
            return Context.Ok<global::app.type.list.@this<global::app.type.item.path.@this>>(
                new global::app.type.list.@this<global::app.type.item.path.@this>(files, Context));
        }
        catch (System.Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException)
        {
            return Context.Error<global::app.type.list.@this<global::app.type.item.path.@this>>(new global::app.error.ServiceError(ex.Message, "IOError", 500));
        }
    }

    public override async Task<data.@this<global::app.type.item.path.@this.StatInfo>> Stat()
    {
        if (await AuthGate(Verb.Read) is { } early) return data.@this<global::app.type.item.path.@this.StatInfo>.From(early);
        if (System.IO.File.Exists(Absolute))
        {
            var info = new System.IO.FileInfo(Absolute);
            return Context.Ok<global::app.type.item.path.@this.StatInfo>(new StatInfo(Exists: true, IsFile: true, Length: info.Length, Modified: info.LastWriteTimeUtc));
        }
        if (System.IO.Directory.Exists(Absolute))
        {
            var info = new System.IO.DirectoryInfo(Absolute);
            return Context.Ok<global::app.type.item.path.@this.StatInfo>(new StatInfo(Exists: true, IsFile: false, Modified: info.LastWriteTimeUtc));
        }
        return Context.Ok<global::app.type.item.path.@this.StatInfo>(new StatInfo(Exists: false));
    }

    // --- Writes --------------------------------------------------------------

    public override async Task<data.@this<global::app.type.item.path.@this>> WriteText(string content)
    {
        if (await AuthGate(Verb.Write) is { } early) return data.@this<global::app.type.item.path.@this>.From(early);
        EnsureParentDir();
        await System.IO.File.WriteAllTextAsync(Absolute, content);
        return Context.Ok<global::app.type.item.path.@this>(this);
    }

    /// <summary>
    /// File-save action target. <paramref name="value"/> may carry bytes,
    /// string, or an arbitrary object (serialized via the actor's
    /// extension-keyed Serializers). Returns the resulting Path wrapped in
    /// Data so the .pr's typed slot round-trips. Replaces today's
    /// <c>file/code/Default.cs::Default.Save</c>.
    /// </summary>
    public override async Task<data.@this<global::app.type.item.path.@this>> Save(data.@this? value)
    {
        if (await AuthGate(Verb.Write) is { } early) return data.@this<global::app.type.item.path.@this>.From(early);

        try
        {
            EnsureParentDir();
            var raw = value == null ? null : await value.Value();
            // A born-native text/binary value rides as its wrapper. Persisted file
            // content IS the value's bare form, so unwrap to the backing here —
            // otherwise it falls to the channel serializer, whose text path appends a
            // stdout-style newline that doesn't belong in file content.
            // Persisted file content IS the value's bare form — the leaf
            // lowers through its own Clr at this System.IO edge.
            if (raw is global::app.type.item.binary.@this binv)
                await System.IO.File.WriteAllBytesAsync(Absolute, binv.Value);
            else if (raw is global::app.type.item.text.@this txtv)
                await System.IO.File.WriteAllTextAsync(Absolute, txtv.Clr<string>());
            else
            {
                await using var stream = System.IO.File.Create(Absolute);
                var serResult = await Context.Actor.Channel.Serializers.SerializeAsync(new global::app.channel.serializer.list.SerializeOptions
                    { Stream = stream, Data = value!, Extension = Extension });
                if (!serResult.Success)
                    return Context.Error<global::app.type.item.path.@this>(serResult.Error!);
            }
            return Context.Ok<global::app.type.item.path.@this>(this);
        }
        catch (System.Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException)
        {
            return Context.Error<global::app.type.item.path.@this>(new global::app.error.ServiceError(ex.Message, "IOError", 500));
        }
        catch (System.Exception ex) when (ex is System.Text.Json.JsonException or System.NotSupportedException)
        {
            return Context.Error<global::app.type.item.path.@this>(new global::app.error.ServiceError(ex.Message, "SerializationError", 500));
        }
    }

    public override async Task<data.@this<global::app.type.item.path.@this>> WriteBytes(byte[] content)
    {
        if (await AuthGate(Verb.Write) is { } early) return data.@this<global::app.type.item.path.@this>.From(early);
        EnsureParentDir();
        await System.IO.File.WriteAllBytesAsync(Absolute, content);
        return Context.Ok<global::app.type.item.path.@this>(this);
    }

    public override async Task<data.@this<global::app.type.item.path.@this>> Append(string content)
    {
        if (await AuthGate(Verb.Write) is { } early) return data.@this<global::app.type.item.path.@this>.From(early);
        EnsureParentDir();
        await System.IO.File.AppendAllTextAsync(Absolute, content);
        return Context.Ok<global::app.type.item.path.@this>(this);
    }

    public override async Task<data.@this<global::app.type.item.path.@this>> Mkdir()
    {
        if (await AuthGate(Verb.Write) is { } early) return data.@this<global::app.type.item.path.@this>.From(early);
        System.IO.Directory.CreateDirectory(Absolute);
        return Context.Ok<global::app.type.item.path.@this>(this);
    }

    // --- Destructive ---------------------------------------------------------

    /// <summary>
    /// Delete with file-action options. Non-recursive directory deletes refuse
    /// non-empty directories with <c>DirectoryNotEmpty</c>; missing targets
    /// surface <c>NotFound</c> unless <paramref name="ignoreIfNotFound"/> is
    /// set. Returns the resulting Path (post-delete) wrapped in Data so the
    /// caller can read <see cref="Exists"/> on it.
    /// </summary>
    public override async Task<data.@this<global::app.type.item.path.@this>> Delete(bool recursive, bool ignoreIfNotFound)
    {
        if (await AuthGate(Verb.Delete) is { } early) return data.@this<global::app.type.item.path.@this>.From(early);
        try
        {
            if (System.IO.File.Exists(Absolute))
                System.IO.File.Delete(Absolute);
            else if (System.IO.Directory.Exists(Absolute))
            {
                if (!recursive && System.IO.Directory.GetFileSystemEntries(Absolute).Length > 0)
                    return Context.Error<global::app.type.item.path.@this>(new global::app.error.ServiceError(
                        $"Directory is not empty: {Raw}. Use recursive=true to delete contents.", "DirectoryNotEmpty", 400));
                System.IO.Directory.Delete(Absolute, recursive);
            }
            else if (!ignoreIfNotFound)
                return Context.Error<global::app.type.item.path.@this>(new global::app.error.ServiceError($"Not found: {Raw}", "NotFound", 404));

            return Context.Ok<global::app.type.item.path.@this>(this);
        }
        catch (System.Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException)
        {
            return Context.Error<global::app.type.item.path.@this>(new global::app.error.ServiceError(ex.Message, "IOError", 500));
        }
    }

    // --- Same-scheme fast paths for Move/Copy --------------------------------

    /// <summary>
    /// Same-scheme move with action-level options. Bundled-consent for the
    /// out-of-root pair stays — calls into BundledTransfer with the overwrite
    /// option threaded through PerformTransfer. Cross-scheme moves fall
    /// through to the base default (ReadBytes → WriteBytes → Delete).
    /// </summary>
    public override async Task<data.@this<global::app.type.item.path.@this>> MoveTo(global::app.type.item.path.@this destination, bool overwrite)
    {
        if (destination is not @this fileDest) return await base.MoveTo(destination, overwrite);
        return await BundledTransfer(fileDest, isMove: true, overwrite: overwrite, includeSubfolders: true);
    }

    /// <summary>
    /// Same-scheme copy with action-level options. See <see cref="MoveTo(global::app.type.item.path.@this, bool)"/>.
    /// </summary>
    public override async Task<data.@this<global::app.type.item.path.@this>> CopyTo(global::app.type.item.path.@this destination, bool overwrite, bool includeSubfolders)
    {
        if (destination is not @this fileDest) return await base.CopyTo(destination, overwrite, includeSubfolders);
        return await BundledTransfer(fileDest, isMove: false, overwrite: overwrite, includeSubfolders: includeSubfolders);
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
    /// same bundled-prompt flow. Backward-compatible default
    /// (overwrite=true, includeSubfolders=true) matches the prior
    /// permission-only behavior.
    /// </summary>
    private async Task<data.@this<global::app.type.item.path.@this>> BundledTransfer(@this destination, bool isMove, bool overwrite = true, bool includeSubfolders = true)
    {
        var sourceVerb = Verb.Read;
        var destVerb   = Verb.Write;

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
            sb.Append(Context.Actor!.Name).Append(" wants to:");
            if (!sourceOk) sb.Append("\n  - read ").Append(Absolute);
            if (!destOk)   sb.Append("\n  - write ").Append(destination.Absolute);
            sb.Append("\n(y/n/a — covers all)");

            var askAction = new module.output.ask(Context)
            {
                Question = new data.@this<global::app.type.item.text.@this>("", sb.ToString()),
            };
            var askResult = await Context.App.Run(askAction, Context);

            if (askResult.ShouldExit()) return data.@this<global::app.type.item.path.@this>.From(askResult);
            if (!askResult.Success) return data.@this<global::app.type.item.path.@this>.From(askResult);

            var ask = await askResult.Value() as global::app.module.output.Ask;
            var answer = ask?.Answer?.Trim();
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
                        ? new global::app.error.PermissionDenied(BuildRequest(Context.Actor!, sourceVerb))
                        : new global::app.error.PermissionDenied(BuildRequest(Context.Actor!, destVerb));
                    return Context.Error<global::app.type.item.path.@this>(denied);
                default:
                    prefix = $"Invalid answer '{answer}'. ";
                    continue;
            }
        }
    }

    private async Task<data.@this?> TryAuthorizeWithoutAsk(Verb verb)
    {
        if (IsInRoot()) return Context.Ok();
        var existing = await Context.Actor!.Permission.Find(this, verb);
        return existing != null ? Context.Ok() : null;
    }

    private async Task StoreGrant(Verb verb, bool persist)
    {
        var grant = BuildRequest(Context.Actor!, verb);
        var d = new data.@this<global::app.type.item.permission.@this>("", grant, context: Context);
        // Signing is at the I/O boundary now: a persisted grant is signed when it
        // crosses application/plang into the settings store. `persist` carries intent.
        await Context.Actor!.Permission.Add(d, persist);
    }

    /// <summary>
    /// Performs the same-scheme transfer post-authorization. Handles both
    /// files and directories with action-handler options (overwrite, recursive
    /// subfolders) — absorbs <c>file/code/Default.cs::Default.Copy/Move</c>.
    /// Returns the new Path (post-transfer) wrapped in Data, with Source set
    /// to the original Absolute so action-handler consumers can read it.
    /// </summary>
    private Task<data.@this<global::app.type.item.path.@this>> PerformTransfer(@this destination, bool isMove, bool overwrite, bool includeSubfolders)
    {
        try
        {
            if (!System.IO.File.Exists(Absolute) && !System.IO.Directory.Exists(Absolute))
                return Task.FromResult(Context.Error<global::app.type.item.path.@this>(new global::app.error.ServiceError($"Not found: {Raw}", "NotFound", 404)));

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
                    return Task.FromResult(
                        Context.Ok<global::app.type.item.path.@this>(new @this(destination.Absolute, Context)));
                }

                CopyDirectory(Absolute, destination.Absolute, overwrite, includeSubfolders);
                return Task.FromResult(
                    Context.Ok<global::app.type.item.path.@this>(new @this(destination.Absolute, Context)));
            }

            // File transfer -----------------------------------------------------
            var destPath = ResolveDestinationPath(this, destination);
            var destDir = PathHelper.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(destDir) && !System.IO.Directory.Exists(destDir))
                System.IO.Directory.CreateDirectory(destDir);

            if (isMove) System.IO.File.Move(Absolute, destPath, overwrite);
            else        System.IO.File.Copy(Absolute, destPath, overwrite);

            return Task.FromResult(
                Context.Ok<global::app.type.item.path.@this>(new @this(destPath, Context)));
        }
        catch (System.Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException)
        {
            return Task.FromResult(Context.Error<global::app.type.item.path.@this>(new global::app.error.ServiceError(ex.Message, "IOError", 500)));
        }
    }
}
