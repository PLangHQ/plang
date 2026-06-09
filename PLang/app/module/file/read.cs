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

    // Opens a file channel and reads through the one boundary — `channel.read`
    // stamps {type, kind} from the file's Mime and returns LAZY Data. Nothing is
    // converted at read time; the value (string / dict / image / table / bytes)
    // materializes on first touch through the reader registry. So `read
    // config.json` untouched stays the raw json string; navigating it parses.
    public async Task<data.@this> Run()
    {
        // Resolve the path door first; the guard reads .Success AFTER the await —
        // resolution errors (bad scheme, unset %var%) only surface once the door
        // has run, so a pre-await guard would inspect an unresolved Data.
        var path = await Path.Value();
        if (!Path.Success) return Path;   // typed scheme error, not an NRE
        var channel = new global::app.channel.type.file.@this(path!);
        var read = await channel.Read();
        if (!read.Success || read.Type?.ClrType.Exit() == true) return read;

        // A file-backed image carries a source-path facet (image.Path → the
        // file, so %img.Path.Exists% works) that only the read site knows — the
        // generic byte→image reader can't recover it. Build the path-backed image
        // here from the raw bytes (no decode); every other type stays lazy.
        if (read.Type?.Name == "image" && read.Raw is byte[] imageBytes)
            return new data.@this(read.Name,
                new global::app.type.image.@this(imageBytes, path!), read.Type);

        // ResolveVariables is an explicit opt-in that needs the text in hand, so
        // it forces materialization and resolves %var% — the only non-lazy path.
        if ((await ResolveVariables.Value())?.Value == true && await read.Value() is string content)
        {
            var resolved = Context.Variable.Resolve(content, skipInfrastructure: true);
            return new data.@this(read.Name, resolved, read.Type);
        }
        return read;
    }

    /// <summary>
    /// Compile-time hint: infer the terminal variable.set's Type from a literal
    /// Path. "foo.csv" → "csv", "data.json" → "json". Variable references and
    /// unknown extensions yield bare Ok() (runtime fills in via MIME dispatch).
    /// A literal path that doesn't exist on disk surfaces a BuildWarning on
    /// Channel("builder") but still returns the inferred type — missing files
    /// are non-fatal at build time.
    /// </summary>
    public async Task<data.@this> Build()
    {
        // Peek the raw .pr value first — Path.Value would trigger resolution on
        // a "%var%" reference that has no binding yet at build time.
        var raw = __action?.Parameters?.FirstOrDefault(p =>
            string.Equals(p.Name, "Path", System.StringComparison.OrdinalIgnoreCase))?.GetValue<string>();
        if (string.IsNullOrEmpty(raw) || raw.Contains('%')) return data.@this.Ok();

        var p = await Path.Value();
        if (p == null || string.IsNullOrEmpty(p.Extension)) return data.@this.Ok();
        if (p.MimeType == "application/octet-stream") return data.@this.Ok();

        // Stage 6: the SAME shared derivation the runtime (FilePath.ReadText)
        // uses — so the build-time stamp and the runtime stamp can't drift.
        // `read foo.md` → {text, md}, `read data.json` → {object, json},
        // `read pic.png` → {image, png}. The terminal variable.set carries the
        // structured type, not a bare extension string.
        var inferred = Context.App.Format.TypeFromExtension(p.Extension);
        if (inferred.IsNull || Context.App.Type.Get(inferred.Name) == null) return data.@this.Ok();
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
