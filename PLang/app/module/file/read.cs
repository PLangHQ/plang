using app.variable;
using app.type;
using app.type.catalog;

namespace app.module.file;

/// <summary>
/// Reads a file and returns its content as Data.
/// When ResolveVariables is true, %var% patterns in the content are resolved
/// (with infrastructure variables blocked for security).
///
/// The Authorize call lives inside the Path verb impl (FilePath.ReadText etc.) —
/// the handler no longer carries an authorization preamble. This is the
/// codeanalyzer v2 #1 fix: gate centralised, not duplicated.
/// </summary>
[Action("read")]
public partial class Read : IContext
{
    public partial data.@this<path> Path { get; init; }

    [Default(false)]
    public partial data.@this<global::app.type.@bool.@this> ResolveVariables { get; init; }

    // `read X` yields a REFERENCE — a `file` (local), `url` (remote), or
    // `directory` — with NOTHING read: existence is verified by a stat (so a
    // missing path still errors at the read step), but the content stays on
    // disk until first examination, where the value door reads + parses +
    // narrows the Data to the content's type. A recognised specialisation
    // (image) stays eager — its content type is known up-front and terminal.
    public async Task<data.@this> Run()
    {
        // Resolve the path door first; the guard reads .Success AFTER the await —
        // resolution errors (bad scheme, unset %var%) only surface once the door
        // has run, so a pre-await guard would inspect an unresolved Data.
        var path = await Path.Value();
        if (!Path.Success) return Path;   // typed scheme error, not an NRE

        // Remote scheme → a url reference. No fetch — consent and I/O land at
        // first examination through the door.
        if (path is global::app.type.path.http.@this)
            return new data.@this("url", new global::app.type.url.@this(path!),
                global::app.type.@this.Create("url", path!.Extension is { Length: > 0 } ue ? ue.TrimStart('.') : null, context: Context))
                { Context = Context };

        // Stat once: NotFound surfaces at the read step (not at first touch),
        // and the stat tells file from directory.
        var stat = await path!.Stat();
        if (!stat.Success) return stat;
        var info = await stat.Value();
        if (info is not { Exists: true })
            return data.@this.FromError(new global::app.error.ServiceError(
                $"Not found: {path}", "NotFound", 404));

        if (info.IsFile == false)
            return new data.@this("directory", new global::app.type.directory.@this(path),
                global::app.type.@this.Create("directory", null, context: Context)) { Context = Context };

        // The plang container (.pr) IS structured Data — a Goal, not content to
        // narrow. Deserialize eagerly through the channel as before.
        if (path.MimeType.StartsWith("application/plang", StringComparison.OrdinalIgnoreCase))
        {
            var prChannel = new global::app.channel.type.file.@this(path);
            return await prChannel.Read();
        }

        // A file-backed image carries a source-path facet (image.Path → the
        // file, so %img.Path.Exists% works) that only the read site knows. Build
        // it eagerly — image is terminal, its content type is already known.
        var mimeType = Context.App.Format?.TypeFromMime(path.MimeType);
        if (mimeType?.Name == "image")
        {
            var channel = new global::app.channel.type.file.@this(path);
            var read = await channel.Read();
            if (!read.Success || read.Type?.ClrType.Exit() == true) return read;
            if (read.Raw is byte[] imageBytes)
                return new data.@this(read.Name,
                    new global::app.type.image.@this(imageBytes, path), read.Type);
            return read;
        }

        // ResolveVariables is an explicit opt-in that needs the text in hand, so
        // it forces materialization and resolves %var% — the only non-lazy path.
        if ((await ResolveVariables.Value())?.Value == true)
        {
            var channel = new global::app.channel.type.file.@this(path);
            var read = await channel.Read();
            if (!read.Success) return read;
            if (await read.Value() is string content)
            {
                var resolved = await Context.Variable.Resolve(content, skipInfrastructure: true);
                return new data.@this(read.Name, resolved, read.Type);
            }
            return read;
        }

        // The reference: the extension rides as the kind (the content-kind
        // inference input — `.json` narrows to dict, `.csv` to table/list).
        var kind = path.Extension is { Length: > 0 } ext ? ext.TrimStart('.') : null;
        return new data.@this(path.FileName, new global::app.type.file.@this(path),
            global::app.type.@this.Create("file", kind, context: Context)) { Context = Context };
    }

    /// <summary>
    /// Compile-time hint: a read of a literal local path lands a `file`
    /// reference whose kind is the extension — the terminal variable.set
    /// carries {file, ext} so it stores the reference as-is (the content type
    /// only appears at runtime, when examination narrows). Variable references
    /// and unknown extensions yield bare Ok(). A recognised eager
    /// specialisation (image) keeps its structured stamp. A literal path that
    /// doesn't exist on disk surfaces a BuildWarning on Channel("builder") but
    /// still returns the inferred type — missing files are non-fatal at build
    /// time.
    /// </summary>
    public async Task<data.@this> Build()
    {
        // Peek the raw .pr value first — Path.Value would trigger resolution on
        // a "%var%" reference that has no binding yet at build time.
        var raw = __action?.Parameters?.FirstOrDefault(p =>
            string.Equals(p.Name, "Path", System.StringComparison.OrdinalIgnoreCase))?.Peek()?.ToString();
        if (string.IsNullOrEmpty(raw) || raw.Contains('%')) return data.@this.Ok();

        var p = await Path.Value();
        if (p == null || string.IsNullOrEmpty(p.Extension)) return data.@this.Ok();
        if (p.MimeType == "application/octet-stream") return data.@this.Ok();

        // The SAME shared derivation the runtime uses, so build-time and
        // runtime stamps can't drift: an image keeps its structured {image,
        // png} stamp (eager specialisation); everything else is the reference
        // — {file, <ext>} — and the content type appears only when runtime
        // examination narrows.
        var inferred = p.Kind;
        if (inferred.IsNull || Context.App.Type.Get(inferred.Name) == null) return data.@this.Ok();
        if (inferred.Name != "image")
            inferred = global::app.type.@this.Create("file", p.Extension.TrimStart('.'), context: Context);
        inferred.Context = Context;

        // Best-effort missing-file warning. Channel("builder") falls back to a
        // no-op sink when no build is active, so this is safe outside builds.
        try
        {
            var exists = await p.ExistsAsync();
            if (exists.Success && (await exists.Value())?.Value == false)
            {
                var warning = new global::app.module.builder.warning.@this(
                    this, $"file.read: literal path '{raw}' does not exist on disk");
                await Context.Actor.Channel.Channel("builder").WriteAsync(data.@this.Ok(warning));
            }
        }
        catch (System.Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException)) { /* best-effort warning — never block Build() */ }

        return data.@this.Ok(inferred);
    }
}
