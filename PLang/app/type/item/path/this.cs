using app.Utils;

namespace app.type.item.path;

/// <summary>
/// Plain domain class representing a filesystem path.
/// NOT a Data subclass — wrapped in Data&lt;Path&gt; by handlers.
/// Stores facts about itself only — the location as typed and its absolute form.
/// Everything that needs a running scope (the root, the formats, the actor's permission)
/// takes the caller's context.
/// </summary>
[global::app.Attributes.PlangType("path"), global::app.Attributes.Kinds]
public abstract partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>Catalog example — read via reflection by the schema builder.</summary>

    /// <summary>
    /// Scheme name for this path (e.g. "file", "http", "https"). Subclasses
    /// implement. Used by Permission canonical-form and diagnostic surfaces.
    /// </summary>
    [Out, Store] public abstract string Scheme { get; }

    /// <summary>A path value's type is "path"; its scheme ("file", "http") is the Kind — so
    /// every scheme variant answers <c>is path</c> by name (no CLR-inheritance lattice), and a
    /// value that narrowed from a path (an image) carries this "path" entry in its type history.</summary>
    protected internal override global::app.type.@this Type
        => new global::app.type.@this("path", typeof(@this), Scheme);


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
    // A location is the text it was given, never a template: a listed file's
    // name, a wire value, a string taken as a path stay as written. A developer's
    // `read %dir%/x.json` is a template SOURCE of type path, filled through text
    // at the source's door and then made a path from what it rendered (source.cs).
    private readonly string _location;

    // The resolved host form, given at construction (file anchors relatives to
    // the goal folder AT RESOLVE TIME — the anchor is call-stack state, so it
    // cannot be derived later). Cached string-derived properties below it.
    private readonly string _absolute;
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
        // construction is both at once.
        _location = path ?? string.Empty;
        _absolute = _location;
    }

    /// <summary>THE PURE CORE — a <c>path</c> passes through; construction from a string needs the
    /// scheme registry (a context), so it lives in the courier below. Any non-path value declines
    /// (<c>null</c>) here. path isn't rank-compared, so the core is never a coercion door.</summary>
    public static @this? Create(object? value) => value as @this;

    /// <summary>The ICreate courier face — a <c>path</c> passes through; a string builds a scheme
    /// path via <see cref="Resolve"/> (uses <c>data.Context</c>); a wrong type or an unregistered
    /// scheme declines with the reason on <paramref name="data"/>.</summary>
    public static @this? Create(object? value, global::app.type.@this? declared, global::app.data.@this data)
    {
        if (value is @this self) return self;
        if (((value as global::app.type.item.@this)?.Clr<object>() ?? value) is not string raw)
        {
            data.Fail(new global::app.error.Error($"Cannot convert {((value as global::app.type.item.@this)?.Type.Name ?? value?.GetType().Name)} to path.", "PathConversionFailed", 400));
            return null;
        }
        try { return Resolve(raw, data.Context!); }
        catch (global::app.error.AppException ex)
        {
            data.Fail(ex.Error);
            return null;
        }
        catch (System.Exception ex) when (ex is not (System.NullReferenceException or System.OutOfMemoryException or System.StackOverflowException))
        {
            data.Fail(new global::app.error.Error(ex.Message, "PathConstructionFailed", 400));
            return null;
        }
    }

    /// <summary>Source generator convention — auto-wraps string parameters.</summary>
    /// <summary>
    /// Source generator convention — auto-wraps string parameters. The raw path's scheme is one
    /// of path's kinds, which builds the right subclass (file → FilePath, http → HttpPath, ...).
    /// Bare paths are file. A scheme that is not one of path's kinds throws
    /// <see cref="scheme.SchemeNotRegistered"/>.
    /// </summary>
    public static @this Resolve(string rawPath, actor.context.@this context)
    {
        ArgumentNullException.ThrowIfNull(rawPath);
        ArgumentNullException.ThrowIfNull(context);
        var name = scheme.@this.ParseScheme(rawPath) is { Length: > 0 } s ? s : "file";
        return context.App.type.list[global::app.type.item.@this.NameOf(typeof(@this))].kind[name] is scheme.@this kind
            ? kind.Create(rawPath, context)
            : throw new scheme.SchemeNotRegistered(name);
    }

    // --- Path properties ---

    /// <summary>
    /// The as-typed location. Init-setting it (the scheme factories do, with the
    /// string the user wrote) makes the verbatim form the value's identity while
    /// the resolved form stays a cached derivation.
    /// </summary>
    public string Raw { get => _location; init { if (!string.IsNullOrEmpty(value)) _location = value; } }

    // The resolved host form — INTERNAL: the raw string is the interop inch
    // (sqlite, Assembly.LoadFrom, HttpClient), reached through the type's own
    // gated edge, never the public navigable surface. The public projection is
    // `!absolute` (derived; leaks the install root, so it stays off the wire).
    internal virtual string Absolute => _absolute;

    /// <summary>The same location written from the root of the asker's app — <c>%p.relative%</c>, a path, so it chains
    /// (<c>%config!path.relative%</c>). The root is the asker's, so it is derived per ask, never kept. A location with
    /// no place under a root (a url) is written as it is.</summary>
    [LlmBuilder] public virtual @this Relative(actor.context.@this context) => this;

    /// <summary>The location's extension, without its dot (<c>json</c>) — <c>%p.extension%</c>; empty for a folder.</summary>
    [LlmBuilder] public virtual global::app.type.item.text.@this Extension => _extension ??= PathHelper.GetExtension(_location);
    [LlmBuilder] public string FileName => _fileName ??= PathHelper.GetFileName(_location);
    [LlmBuilder] public string FileNameWithoutExtension
        => _fileNameWithoutExtension ??= PathHelper.GetFileNameWithoutExtension(_location);
    [LlmBuilder] public string Directory => _directory ??= PathHelper.GetDirectoryName(Absolute) ?? Absolute;
    /// <summary>The MIME of this location's extension — the format with that extension says it; a format with
    /// no MIME of its own arrives as its type's (<c>.ini</c> is text/plain); opaque bytes when neither does.</summary>
    [LlmBuilder] public string MimeType(actor.context.@this context)
    {
        var type = Kind(context);
        if (type.IsNull) return "application/octet-stream";
        return type.kind.Mime.FirstOrDefault()
               ?? context.App.type.list[type.Name].kind.Mime.FirstOrDefault()
               ?? "application/octet-stream";
    }

    [LlmBuilder] public bool IsFile => Extension.IsTruthy();
    [LlmBuilder] public bool IsDirectory => !Extension.IsTruthy();

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
        var rel = other.Relative(context).Raw;
        var pathQualified = rel.Contains('/') || rel.Contains('\\');
        if (pathQualified)
        {
            var mine = Relative(context).Raw;
            return mine.EndsWith(rel, StringComparison.OrdinalIgnoreCase)
                || mine.StartsWith(rel, StringComparison.OrdinalIgnoreCase);
        }
        return FileName.Equals(other.FileName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extension → content-kind: the type entity this location's extension
    /// names (<c>.json</c> → the json-kinded entity), from the caller's format registry.
    /// Owned by the path + the type registry.
    /// </summary>
    public global::app.type.@this Kind(actor.context.@this context) =>
        context.App?.type.list.Extension(Extension.ToString(), context) ?? global::app.type.@this.Null;

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
    public override string ToString() => _location;

    /// <summary>
    /// The type owns its wire shape and reads its own private fields — the typed location.
    /// The resolved <see cref="Absolute"/> stays off the wire (it leaks the install root and
    /// is gated behind Authorize).
    /// </summary>
    public override void Write(global::app.type.format.IWriter w) => w.String(ToString());

    /// <summary>A path writes its own form — its location (<see cref="Write"/>) — in every view.</summary>
    public override System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        Write(writer);
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }


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
}
