using app.variable;
using app.type;
using app.type.list;

namespace app.module.action.file;

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
    public partial data.@this<global::app.type.item.@bool.@this> ResolveVariables { get; init; }

    // `read X` yields a REFERENCE — a `file` (local), `url` (remote), or
    // `directory` — with NOTHING read: existence is verified by a stat (so a
    // missing path still errors at the read step), but the content stays on
    // disk until first examination, where the value door reads + parses +
    // narrows the Data to the content's type (an image becomes one when used).
    public async Task<data.@this> Run()
    {
        // Resolve the path door first; the guard reads .Success AFTER the await —
        // resolution errors (bad scheme, unset %var%) only surface once the door
        // has run, so a pre-await guard would inspect an unresolved Data.
        var path = await Path.Value();
        if (!Path.Success) return Path;   // typed scheme error, not an NRE

        // Remote scheme → a url reference. No fetch — consent and I/O land at
        // first examination through the door.
        if (path is global::app.type.item.path.http.@this)
            return new data.@this("url", new global::app.type.item.url.@this(path!, Context),
                Context.App.Type[new global::app.type.@this("url", path!.Extension is { Length: > 0 } ue ? ue.TrimStart('.') : null)],
                context: Context);

        // Stat once: NotFound surfaces at the read step (not at first touch),
        // and the stat tells file from directory.
        var stat = await path!.Stat(Context);
        if (!stat.Success) return stat;
        var info = await stat.Value();
        if (info is not { Exists: true })
            return Context.Error(new global::app.error.ServiceError(
                $"Not found: {path}", "NotFound", 404));

        if (info.IsFile == false)
            return new data.@this("directory", new global::app.type.item.directory.@this(path),
                Context.App.Type["directory"], context: Context);

        // The plang container (.pr) IS structured Data — a Goal, not content to
        // narrow. Deserialize eagerly through the channel as before.
        var mime = path.MimeType(Context);
        if (mime.StartsWith("application/plang", StringComparison.OrdinalIgnoreCase))
        {
            var prChannel = new global::app.channel.type.file.@this(path, Context);
            return await prChannel.Read();
        }

        // ResolveVariables is an explicit opt-in that needs the text in hand, so
        // it forces materialization and resolves %var% — the only non-lazy path.
        if (await ResolveVariables.ToBooleanAsync())
        {
            var channel = new global::app.channel.type.file.@this(path, Context);
            var read = await channel.Read();
            if (!read.Success) return read;
            var content = await read.Value();
            if (content is global::app.type.item.text.@this)
            {
                var resolved = await Context.Variable.Resolve(content.ToString()!, skipInfrastructure: true);
                return new data.@this(read.Name, resolved, read.Type, context: Context);
            }
            return read;
        }

        // The reference: the extension rides as the kind (the content-kind
        // inference input — `.json` narrows to dict, `.csv` to table/list).
        var kind = path.Extension is { Length: > 0 } ext ? ext.TrimStart('.') : null;
        return new data.@this(path.FileName, new global::app.type.item.file.@this(path, Context),
            Context.App.Type[new global::app.type.@this("file", kind)], context: Context);
    }

    /// <summary>
    /// Compile-time hint: a read of a literal local path lands a `file`
    /// reference whose kind is the extension — the terminal variable.set
    /// carries {file, ext} so it stores the reference as-is (the content type
    /// only appears at runtime, when examination narrows). Variable references
    /// and unknown extensions yield bare Ok(). A literal path that
    /// doesn't exist on disk surfaces a {action, message} warning dict on
    /// Channel("builder") but
    /// still returns the inferred type — missing files are non-fatal at build
    /// time.
    /// </summary>
    public async Task<data.@this> Build()
    {
        // A path marked a template holds a variable that has no binding yet at build time — the
        // marker says so, never the characters in it.
        var raw = __action?["Path"]?.Value?.ToString();
        if (string.IsNullOrEmpty(raw) || Path.HasVariableReference) return Context.Ok();

        var p = await Path.Value();
        if (p == null || string.IsNullOrEmpty(p.Extension)) return Context.Ok();
        if (p.MimeType(Context) == "application/octet-stream") return Context.Ok();

        // The same reference the runtime lands — {file, <ext>} — so build-time and runtime
        // stamps can't drift; the content type appears only when runtime examination narrows.
        var inferred = Context.App.Type[new global::app.type.@this("file", p.Extension.TrimStart('.'))];

        // Best-effort missing-file warning. Channel("builder") falls back to a
        // no-op sink when no build is active, so this is safe outside builds.
        try
        {
            var exists = await p.ExistsAsync(Context);
            if (exists.Success && !await exists.ToBooleanAsync())
            {
                // Advisory build warning as a native dict {action, message} —
                // `action` is the source attribution (the handler reduces to its
                // own identity; a live handler has no wire form).
                string source = __action == null ? "" : $"{__action.Module}.{__action.Name}";
                var warning = new global::app.type.item.dict.@this()
                    .Set("action", source)
                    .Set("message", $"file.read: literal path '{raw}' does not exist on disk");
                await Context.Actor.Channel.Channel("builder").WriteAsync(Context.Ok(warning));
            }
        }
        catch (System.Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException)) { /* best-effort warning — never block Build() */ }

        return Context.Ok(inferred);
    }
}
