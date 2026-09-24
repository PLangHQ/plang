using app.Utils;

namespace app.type.item.path;

/// <summary>
/// Plain domain class representing a filesystem path.
/// NOT a Data subclass — wrapped in Data&lt;Path&gt; by handlers.
/// Stores facts about itself only — the location as typed and its absolute form.
/// Everything that needs a running scope (the root, the formats, the actor's permission)
/// takes the caller's context.
/// </summary>
public abstract partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>Catalog example — read via reflection by the schema builder.</summary>
    public static string Example => "/some/file.json";

    /// <summary>
    /// Scheme name for this path (e.g. "file", "http", "https"). Subclasses
    /// implement. Used by Permission canonical-form and diagnostic surfaces.
    /// </summary>
    [Out, Store] public abstract string Scheme { get; }

    /// <summary>A path value's type is "path"; its scheme ("file", "http") is the Kind — so
    /// every scheme variant answers <c>is path</c> by name (no CLR-inheritance lattice), and a
    /// value that narrowed from a path (an image) carries this "path" entry in its type history.</summary>
    protected internal override global::app.type.@this Type
        => new global::app.type.@this("path") { Kind = new global::app.type.kind.@this(Scheme) };


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
    private global::app.type.item.text.@this _location;

    // Cached string-derived properties. _absolute is primed at construction by
    // schemes that resolve eagerly (file anchors relatives to the goal folder
    // AT RESOLVE TIME — the anchor is call-stack state, so it cannot be derived
    // later).
    private string? _absolute;
    private string? _extension;
    private string? _fileName;
    private string? _fileNameWithoutExtension;
    private string? _directory;

    /// <summary>
    /// Creates a Path from its location. The scheme factories resolve it with the creating
    /// context (the running goal's folder, the root) and hand the resolved form here; nothing
    /// of that context is kept.
    /// </summary>
    protected @this(string path)
    {
        // Producers that resolved the path hand the resolved form here and
        // override the as-typed location via the Raw init; a verbatim
        // construction is both at once. path authorizes templating — text decides
        // whether the location actually carries a %var%. Mode is "plang" here: a
        // path location is template-capable (mode-gating path by the reader is a
        // later step; this preserves the prior behavior).
        _location = new global::app.type.item.text.@this(path, "plang");
        // A template location has no resolved host form yet — leave _absolute
        // unprimed until Value renders it. A literal location is resolved as-is.
        _absolute = _location.Template == null ? path : null;
    }

    /// <summary>THE PURE CORE — a <c>path</c> passes through; construction from a string needs the
    /// scheme registry (a context), so it lives in the courier below. Any non-path value declines
    /// (<c>null</c>) here. path isn't rank-compared, so the core is never a coercion door.</summary>
    public static @this? Create(object? value) => value as @this;

    /// <summary>The ICreate courier face — a <c>path</c> passes through; a string builds a scheme
    /// path via <c>Scheme.From</c> (uses <c>data.Context</c>); a wrong type or an unregistered
    /// scheme declines with the reason on <paramref name="data"/>.</summary>
    public static @this? Create(object? value, global::app.data.@this data)
    {
        if (value is @this self) return self;
        if (((value as global::app.type.item.@this)?.Clr<object>() ?? value) is not string raw)
        {
            data.Fail(new global::app.error.Error($"Cannot convert {((value as global::app.type.item.@this)?.Type.Name ?? value?.GetType().Name)} to path.", "PathConversionFailed", 400));
            return null;
        }
        try { return data.Context.App.Type.Scheme.From(raw, data.Context); }
        catch (scheme.SchemeNotRegistered snr)
        {
            data.Fail(new global::app.error.Error(snr.Message, "SchemeNotRegistered", 400)
                { FixSuggestion = $"Register a factory for scheme '{snr.Scheme}' via app.Type.Scheme.Register, or use a bare/file:// path." });
            return null;
        }
        catch (System.Exception ex) when (ex is not (System.NullReferenceException or System.OutOfMemoryException or System.StackOverflowException))
        {
            data.Fail(new global::app.error.Error(ex.Message, "PathConstructionFailed", 400));
            return null;
        }
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
        return Resolve(rendered, data.Context);
    }

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
    public string Raw { get => _location.Clr<string>() ?? ""; init { if (!string.IsNullOrEmpty(value)) _location = new global::app.type.item.text.@this(value, "plang"); } }

    // The resolved host form — INTERNAL: the raw string is the interop inch
    // (sqlite, Assembly.LoadFrom, HttpClient), reached through the type's own
    // gated edge, never the public navigable surface. The public projection is
    // `!absolute` (derived; leaks the install root, so it stays off the wire).
    internal virtual string Absolute => _absolute ??= _location.Clr<string>() ?? "";

    // INTERNAL: the raw relative string feeds IsUnder/Matches + the `!relative`
    // derived projection; consumers do containment through those, not string math.
    // Relative to the root of the caller's app — the root is the caller's, so it is
    // derived per ask, never kept.
    internal string Relative(actor.context.@this context)
    {
        // App not wired yet (bootstrap, before runtime is up) — no root
        // anchor, so the portable form is the as-typed location.
        var rootAbsolutePath = context.App?.AbsolutePath;
        if (rootAbsolutePath == null) return _location.Clr<string>() ?? "";

        var rootWithSeparator = rootAbsolutePath;
        if (!rootWithSeparator.EndsWith(PathHelper.DirectorySeparatorChar) && !rootWithSeparator.EndsWith(PathHelper.AltDirectorySeparatorChar))
            rootWithSeparator += PathHelper.DirectorySeparatorChar;

        // Canonical PLang root-relative form: leading "/" anchors at the
        // app root, "/" as separator regardless of OS (matches Goal.Path
        // / GoalCall.PrPath stored in .pr files). Out-of-root paths
        // return their Absolute form unchanged — those aren't "relative
        // to root" in any meaningful sense.
        if (Absolute.StartsWith(rootWithSeparator, RootComparison))
            return "/" + Absolute[rootWithSeparator.Length..].Replace('\\', '/');
        if (string.Equals(Absolute, rootAbsolutePath, RootComparison))
            return "/";
        return Absolute;
    }

    // INTERNAL: the raw extension feeds Kind + the `!extension` projection.
    internal string Extension => _extension ??= PathHelper.GetExtension(_location.Clr<string>() ?? "");
    [LlmBuilder] public string FileName => _fileName ??= PathHelper.GetFileName(_location.Clr<string>() ?? "");
    [LlmBuilder] public string FileNameWithoutExtension
        => _fileNameWithoutExtension ??= PathHelper.GetFileNameWithoutExtension(_location.Clr<string>() ?? "");
    [LlmBuilder] public string Directory => _directory ??= PathHelper.GetDirectoryName(Absolute) ?? Absolute;
    /// <summary>The mime type of this location's extension, from the caller's format registry.</summary>
    [LlmBuilder] public string MimeType(actor.context.@this context) => context.App?.Format?.Mime(Extension) ?? "application/octet-stream";

    [LlmBuilder] public bool IsFile => !string.IsNullOrEmpty(Extension);
    [LlmBuilder] public bool IsDirectory => string.IsNullOrEmpty(Extension);

    // --- Typed surface (the navigable plane answers in PLang values; the
    //     interior string-math lives HERE, on the owner) ---

    /// <summary>
    /// Containment: does this path live under <paramref name="root"/>? The
    /// typed query that replaces consumer-side <c>Relative.StartsWith</c>
    /// string math. Same root-comparison rule the permission gate uses.
    /// </summary>
    public global::app.type.item.@bool.@this IsUnder(@this root)
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
    public global::app.type.item.@bool.@this Matches(@this other, actor.context.@this context)
    {
        var rel = other.Relative(context);
        var pathQualified = rel.Contains('/') || rel.Contains('\\');
        if (pathQualified)
        {
            var mine = Relative(context);
            return mine.EndsWith(rel, StringComparison.OrdinalIgnoreCase)
                || mine.StartsWith(rel, StringComparison.OrdinalIgnoreCase);
        }
        return FileName.Equals(other.FileName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extension → content-kind: the type entity this location's extension
    /// names (<c>.json</c> → the json-kinded entity), from the caller's format registry.
    /// Owned by the path + the format registry — replaces consumer-side
    /// <c>Format.TypeFromExtension(p.Extension)</c>.
    /// </summary>
    public global::app.type.@this Kind(actor.context.@this context) =>
        context.App?.Format?.TypeFromExtension(Extension) ?? global::app.type.@this.Null;

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
    // The location is typed text: as the developer wrote it, or — for a derived path
    // (Parent/Combine/move results) — derived from its source's typed text by the same
    // string math that derives its absolute. So a path shows one way everywhere and the
    // install root never shows. The root-relative form (Relative) is for `!relative` and
    // comparisons, not display.
    public override string ToString() => _location.Clr<string>() ?? "";

    /// <summary>
    /// The type owns its wire shape and reads its own private fields — the typed location.
    /// The resolved <see cref="Absolute"/> stays off the wire (it leaks the install root and
    /// is gated behind Authorize).
    /// </summary>
    public override void Write(global::app.channel.serializer.IWriter w) => w.String(ToString());

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
