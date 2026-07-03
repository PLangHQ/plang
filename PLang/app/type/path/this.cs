using app.Utils;

namespace app.type.path;

/// <summary>
/// Plain domain class representing a filesystem path.
/// NOT a Data subclass — wrapped in Data&lt;Path&gt; by handlers.
/// Implements IContext for runtime graph access (FileSystem, etc.).
/// </summary>
public abstract partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>, module.IContext
{
    /// <summary>Catalog example — read via reflection by the schema builder.</summary>
    public static string Example => "/some/file.json";

    /// <summary>
    /// Scheme name for this path (e.g. "file", "http", "https"). Subclasses
    /// implement. Used by Permission canonical-form and diagnostic surfaces.
    /// </summary>
    [Out, Store] public abstract string Scheme { get; }


    /// <summary>
    /// String comparison for "is this path under that root" checks. Linux
    /// filesystems are case-sensitive — comparing case-insensitively lets
    /// <c>/SRV/myapp</c> match <c>/srv/myapp</c> and slip past the gate.
    /// Windows is case-insensitive at the filesystem layer, so we honour
    /// that. Single home so <see cref="IsUnder"/> and
    /// <c>PLangFileSystem.ValidatePath</c> can't drift apart again.
    /// </summary>
    internal static StringComparison RootComparison =>
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    // The backing is the LOCATION — the string the user gave, verbatim:
    // "//file.txt" (host-OS root), "/file.txt" (app root), "file.txt" /
    // "test/try.txt" (relative), "c:/my/path.txt" (absolute), "http://…" (url).
    // Everything else (absolute, relative, extension, …) is derived from it per
    // the scheme's resolution rules and cached. Private — the wire form comes
    // from Write reading it directly; no public raw accessor leaks it.
    // The LOCATION as a text value — so a `%var%` location rides as a template
    // and renders at the door (Value), exactly like any other text. The string
    // accessors below lower it through Clr<string>() for their path math; by the
    // time any accessor runs the navigation has already called Value(), which
    // resolved the template (see Value / Cacheable below).
    private global::app.type.text.@this _location;

    // Cached string-derived properties. _absolute is primed at construction by
    // schemes that resolve eagerly (file anchors relatives to the goal folder
    // AT RESOLVE TIME — the anchor is call-stack state, so it cannot be derived
    // later).
    private string? _absolute;
    private string? _extension;
    private string? _fileName;
    private string? _fileNameWithoutExtension;
    private string? _directory;
    private string? _relative;

    /// <summary>
    /// App root directory — resolved from Context. A path is always born with
    /// Context (the ctors require it); the string-derived properties (Relative,
    /// Extension, …) still throw if App itself isn't wired yet.
    /// </summary>
    private string RootAbsolutePath => Context.App?.AbsolutePath
        ?? throw new InvalidOperationException(
            "Path requires Context with App — wire it before accessing path-derived properties");

    /// <summary>
    /// Creates a Path born with its actor Context — every scheme subclass
    /// requires it, so a path resolves scheme-correct and fully wired the
    /// moment it lands. The public setter stays for IContext scope propagation.
    /// </summary>
    protected @this(string path, actor.context.@this context)
    {
        // Producers that resolved the path hand the resolved form here and
        // override the as-typed location via the Raw init; a verbatim
        // construction is both at once. path authorizes templating — text decides
        // whether the location actually carries a %var%. Mode is "plang" here: a
        // path location is template-capable (mode-gating path by the reader is a
        // later step; this preserves the prior behavior).
        _location = new global::app.type.text.@this(path, "plang");
        // A template location has no resolved host form yet — leave _absolute
        // unprimed until Value renders it. A literal location is resolved as-is.
        _absolute = _location.Template == null ? path : null;
        Context = context;
    }

    /// <summary>Caching follows the location text: a template location depends on
    /// outside %vars% so it must re-resolve each use (text answers not-cacheable);
    /// a literal location is stable. path defers to text — it owns that judgement.</summary>
    public override bool Cacheable => _location.Cacheable;

    /// <summary>Final when the location is literal — Value() returns this. A template
    /// location resolves through text's door, so it is not final.</summary>
    internal override bool IsFinal => _location.Cacheable;

    /// <summary>
    /// THE door. A literal location answers itself. A template location renders
    /// through text's door (full-match %var% → the variable's value, partial →
    /// interpolated string) and re-resolves the rendered string into a fresh,
    /// resolved path via the scheme registry — so the result is a normal path
    /// whose sync accessors are safe. Never mutates this shared instance.
    /// </summary>
    public override async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Value(global::app.data.@this data)
    {
        if (_location.Cacheable) return this;   // literal location — already resolved
        var rendered = (await _location.Value(data)).Clr<string>() ?? "";
        return Resolve(rendered, data.Context ?? Context!);
    }

    /// <summary>
    /// Context for runtime access. Settable through IContext (Data propagates
    /// it automatically when Path is inside Data&lt;Path&gt;).
    /// JsonIgnore — Context references the runtime graph (App, Culture, parents)
    /// which contains cycles; serializing it blows up trace files.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public actor.context.@this Context { get; set; } = null!;

    /// <summary>Source generator convention — auto-wraps string parameters.</summary>
    /// <summary>
    /// Source generator convention — auto-wraps string parameters. Routes
    /// through the per-App scheme registry so the right subclass is built
    /// (file → FilePath, http → HttpPath, ...). Bare paths default to file.
    /// </summary>
    public static @this Resolve(string rawPath, actor.context.@this context)
    {
        ArgumentNullException.ThrowIfNull(rawPath);
        ArgumentNullException.ThrowIfNull(context);
        return context.App.Type.Scheme.From(rawPath, context);
    }

    // --- Path properties ---

    /// <summary>
    /// The as-typed location. Init-setting it (the scheme factories do, with the
    /// string the user wrote) makes the verbatim form the value's identity while
    /// the resolved form stays a cached derivation.
    /// </summary>
    public string Raw { get => _location.Clr<string>() ?? ""; init { if (!string.IsNullOrEmpty(value)) _location = new global::app.type.text.@this(value, "plang"); } }

    // The resolved host form — INTERNAL: the raw string is the interop inch
    // (sqlite, Assembly.LoadFrom, HttpClient), reached through the type's own
    // gated edge, never the public navigable surface. The public projection is
    // `!absolute` (derived; leaks the install root, so it stays off the wire).
    internal virtual string Absolute => _absolute ??= _location.Clr<string>() ?? "";

    // INTERNAL: the raw relative string feeds IsUnder/Matches + the `!relative`
    // derived projection; consumers do containment through those, not string math.
    internal string Relative
    {
        get
        {
            if (_relative != null) return _relative;

            // App not wired yet (bootstrap, before runtime is up) — no root
            // anchor, so the portable form is the as-typed location.
            if (Context.App == null)
                return _relative = _location.Clr<string>() ?? "";

            var rootAbsolutePath = RootAbsolutePath;
            var rootWithSeparator = rootAbsolutePath;
            if (!rootWithSeparator.EndsWith(PathHelper.DirectorySeparatorChar) && !rootWithSeparator.EndsWith(PathHelper.AltDirectorySeparatorChar))
                rootWithSeparator += PathHelper.DirectorySeparatorChar;

            // Canonical PLang root-relative form: leading "/" anchors at the
            // app root, "/" as separator regardless of OS (matches Goal.Path
            // / GoalCall.PrPath stored in .pr files). Out-of-root paths
            // return their Absolute form unchanged — those aren't "relative
            // to root" in any meaningful sense.
            if (Absolute.StartsWith(rootWithSeparator, RootComparison))
                _relative = "/" + Absolute[rootWithSeparator.Length..].Replace('\\', '/');
            else if (string.Equals(Absolute, rootAbsolutePath, RootComparison))
                _relative = "/";
            else
                _relative = Absolute;

            return _relative;
        }
    }

    // INTERNAL: the raw extension feeds Kind + the `!extension` projection.
    internal string Extension => _extension ??= PathHelper.GetExtension(_location.Clr<string>() ?? "");
    [LlmBuilder] public string FileName => _fileName ??= PathHelper.GetFileName(_location.Clr<string>() ?? "");
    [LlmBuilder] public string FileNameWithoutExtension
        => _fileNameWithoutExtension ??= PathHelper.GetFileNameWithoutExtension(_location.Clr<string>() ?? "");
    [LlmBuilder] public string Directory => _directory ??= PathHelper.GetDirectoryName(Absolute) ?? Absolute;
    [LlmBuilder] public string MimeType => Context.App?.Format?.Mime(Extension) ?? "application/octet-stream";

    [LlmBuilder] public bool IsFile => !string.IsNullOrEmpty(Extension);
    [LlmBuilder] public bool IsDirectory => string.IsNullOrEmpty(Extension);

    // --- Typed surface (the navigable plane answers in PLang values; the
    //     interior string-math lives HERE, on the owner) ---

    /// <summary>
    /// Containment: does this path live under <paramref name="root"/>? The
    /// typed query that replaces consumer-side <c>Relative.StartsWith</c>
    /// string math. Same root-comparison rule the permission gate uses.
    /// </summary>
    public global::app.type.@bool.@this IsUnder(@this root)
    {
        var rootAbs = root.Absolute;
        if (string.IsNullOrEmpty(rootAbs)) return false;
        var rootWithSep = rootAbs.EndsWith(PathHelper.DirectorySeparatorChar) || rootAbs.EndsWith(PathHelper.AltDirectorySeparatorChar)
            ? rootAbs
            : rootAbs + PathHelper.DirectorySeparatorChar;
        return Absolute.StartsWith(rootWithSep, RootComparison)
            || string.Equals(Absolute, rootAbs, RootComparison);
    }

    /// <summary>
    /// Affix match for filter-style comparisons: a path-qualified
    /// <paramref name="other"/> matches when this relative form starts or ends
    /// with it; a bare name matches by filename. Case-insensitive — filters
    /// are user-typed.
    /// </summary>
    public global::app.type.@bool.@this Matches(@this other)
    {
        var rel = other.Relative;
        var pathQualified = rel.Contains('/') || rel.Contains('\\');
        if (pathQualified)
            return Relative.EndsWith(rel, StringComparison.OrdinalIgnoreCase)
                || Relative.StartsWith(rel, StringComparison.OrdinalIgnoreCase);
        return FileName.Equals(other.FileName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extension → content-kind: the type entity this location's extension
    /// names (<c>.json</c> → the json-kinded entity). Owned by the path + the
    /// format registry — replaces consumer-side
    /// <c>Format.TypeFromExtension(p.Extension)</c>.
    /// </summary>
    public global::app.type.@this Kind =>
        Context.App?.Format?.TypeFromExtension(Extension) ?? global::app.type.@this.Null;

    /// <summary>
    /// Converts this path to a GoalCall. Derives PrPath from the .goal file path.
    /// Excluded from default JSON serialization — it builds a new GoalCall (which
    /// holds a Path which has a GoalCall ...) and would cycle infinitely when a
    /// caller serializes a Goal without the PathJsonConverter registered.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public GoalCall GoalCall
    {
        get
        {
            // Derive the .pr sibling path via the generic derivation verbs.
            // .goal-file → parent/.build/<lowercase-stem>.pr.
            var stem = FileNameWithoutExtension.ToLowerInvariant();
            var parent = Parent;
            var prPath = parent != null
                ? parent.Combine(".build").Combine(stem + ".pr")
                : this.Combine(".build").Combine(stem + ".pr");
            return new GoalCall { Name = "", PrPath = prPath };
        }
    }

    // --- Live filesystem state ---
    //
    // The base deliberately exposes NO sync live-state property. `Exists` and
    // `Size` used to live here as `System.IO.File.Exists` / `FileInfo` calls —
    // wrong for an HttpPath (always-false / throws on Windows). They moved to
    // FilePath. The cross-scheme liveness query is the async `path.Stat()`;
    // truthiness ("does it exist") is `AsBooleanAsync()`.

    // --- Display + wire ---

    // A path is a LOCATION value — it never carries content (content belongs
    // to the file/url reference types), so its string form is location-only.
    //
    // Portable form: the as-typed location, verbatim. An internally derived
    // path (Combine/Parent/move results) has no as-typed form — its location
    // IS the resolved string — so it collapses to the root-relative form,
    // keeping the install root out of display and off the wire.
    private string Portable
    {
        get
        {
            var loc = _location.Clr<string>() ?? "";
            if (!string.Equals(loc, _absolute, StringComparison.Ordinal)) return loc;
            try { return Relative; } catch { return loc; }
        }
    }

    public override string ToString() => Portable;

    /// <summary>
    /// The type owns its wire shape and reads its own private fields — the
    /// portable location. The resolved <see cref="Absolute"/> stays off the
    /// wire (it leaks the install root and is gated behind Authorize).
    /// </summary>
    public override void Write(global::app.channel.serializer.IWriter w) => w.String(Portable);

    // Path equality follows RootComparison — the same case-sensitivity rule
    // Relative/IsUnder/ValidatePath use, so they can't drift apart. Hard-coding
    // OrdinalIgnoreCase here would make /srv/x and /SRV/x — distinct files on
    // Linux — compare equal and hash-collide.
    public override bool Equals(object? obj) => obj switch
    {
        @this other => string.Equals(Absolute, other.Absolute, RootComparison),
        string str => string.Equals(Absolute, str, RootComparison),
        _ => false
    };

    public override int GetHashCode() =>
        StringComparer.FromComparison(RootComparison).GetHashCode(Absolute);


    /// <summary>
    /// A Path implicitly stringifies to its <see cref="ToString"/> representation.
    /// Lets <c>Assert.That(path).IsEqualTo("/some/path")</c> compile as a
    /// string-vs-string check (with the right value surfaced in failure messages),
    /// and rescues string interpolation across third-party libs that don't call
    /// ToString themselves. Returns null for null Path so null-aware assertions
    /// (e.g. <c>IsNull()</c>) don't get fooled by the implicit conversion into
    /// reading an empty string as "found a value".
    /// </summary>
    public static implicit operator string?(@this? p) => p?.ToString();
}
