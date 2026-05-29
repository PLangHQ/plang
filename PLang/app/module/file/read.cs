using app.variable;
using app.type;
using app.type.list;

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
    public partial data.@this<bool> ResolveVariables { get; init; }

    // Bare Data — polymorphic by MIME (text → string, binary → byte[], json → structured,
    // image → app.type.image.@this). The Type stamp carries the high-level type; the
    // value is the typed instance.
    public async Task<data.@this> Run()
    {
        if (!Path.Success) return Path;   // codeanalyzer v1 F4 — typed scheme error, not an NRE
        var read = await Path.Value!.ReadText();
        if (!read.Success || read.Type?.ClrType.Exit() == true) return read;

        // plang-types Stage 5: when the read result is image-MIME bytes, lift
        // to an image value. Bytes are loaded once here; image is the leaf
        // owner of width/height/mime — width/height stay lazy until accessed.
        var mime = read.Type?.Value ?? "";
        if (read.Value is byte[] bytes && mime.StartsWith("image/", System.StringComparison.OrdinalIgnoreCase))
        {
            var image = new global::app.type.image.@this(bytes, mime, Path.Value);
            return new data.@this(read.Name, image, app.type.@this.FromName("image"));
        }

        if (ResolveVariables.Value && read.Value is string content)
        {
            var resolved = Context.Variables.Resolve(content, skipInfrastructure: true);
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
            string.Equals(p.Name, "Path", System.StringComparison.OrdinalIgnoreCase))?.Value as string;
        if (string.IsNullOrEmpty(raw) || raw.Contains('%')) return data.@this.Ok();

        var p = Path.Value;
        if (p == null || string.IsNullOrEmpty(p.Extension)) return data.@this.Ok();
        if (p.MimeType == "application/octet-stream") return data.@this.Ok();

        // plang-types Stage 5: resolve extension → HIGH-LEVEL type (image, code,
        // …) via app.formats when the high-level name is a registered typed
        // value (image has Build/kind/serializer). For text-shaped extensions
        // (csv/json/xml/yaml/md/txt/ini) keep the extension stamp — they're
        // string aliases that read more clearly than the generic "text".
        var ext = p.Extension.TrimStart('.').ToLowerInvariant();
        string typeName = ext;
        var highLevel = Context.App.Formats.Kind(ext);
        if (highLevel != null
            && !string.Equals(highLevel, "text", System.StringComparison.OrdinalIgnoreCase)
            && Context.App.Types.Get(highLevel) != null)
        {
            typeName = highLevel;
        }
        if (Context.App.Types.Get(typeName) == null) return data.@this.Ok();

        // Best-effort missing-file warning. Channel("builder") falls back to a
        // no-op sink when no build is active, so this is safe outside builds.
        try
        {
            var exists = await p.ExistsAsync();
            if (exists.Success && exists.Value == false)
            {
                var warning = new global::app.module.builder.warning.@this(
                    this, $"file.read: literal path '{raw}' does not exist on disk");
                await Context.Actor.Channels.Channel("builder").WriteAsync(data.@this.Ok(warning));
            }
        }
        catch (System.Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException)) { /* best-effort warning — never block Build() */ }

        return data.@this.Ok(typeName);
    }
}
