using app.variables;
using app.types;

namespace app.modules.file;

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

    // Bare Data — polymorphic by MIME (text → string, binary → byte[], json → structured).
    // The Type stamp from ReadText carries the actual shape.
    public async Task<data.@this> Run()
    {
        if (!Path.Success) return Path;   // codeanalyzer v1 F4 — typed scheme error, not an NRE
        var read = await Path.Value!.ReadText();
        if (!read.Success || read.Type?.ClrType.Exit() == true) return read;
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

        var typeName = p.Extension.TrimStart('.').ToLowerInvariant();

        // Only stamp if the extension is a registered PLang type — otherwise
        // downstream variable.set tries to convert via an unknown type and
        // surfaces "Unknown type 'X'". Common text-shaped extensions (csv, txt,
        // xml, yaml) are registered as string aliases in app.types so they pass
        // this check while still carrying the more-specific annotation downstream.
        if (Context.App.Types.Get(typeName) == null) return data.@this.Ok();

        // Best-effort missing-file warning. Channel("builder") falls back to a
        // no-op sink when no build is active, so this is safe outside builds.
        try
        {
            var exists = await p.ExistsAsync();
            if (exists.Success && exists.Value == false)
            {
                var warning = new global::app.modules.builder.warning.@this(
                    this, $"file.read: literal path '{raw}' does not exist on disk");
                await Context.Actor.Channels.Channel("builder").WriteAsync(data.@this.Ok(warning));
            }
        }
        catch (System.Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException)) { /* best-effort warning — never block Build() */ }

        return data.@this.Ok(typeName);
    }
}
