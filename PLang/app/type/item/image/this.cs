namespace app.type.item.image;

/// <summary>
/// PLang <c>image</c> value — a binary blob plus its MIME type, optionally
/// backed by a source <see cref="app.type.item.path.@this"/>.
///
/// <para><c>image</c> is the proving instance for format-asymmetric dispatch:
/// <c>serializer/Default.cs</c> renders base64 (json + plang); <c>text.cs</c>
/// renders a path placeholder; <c>protobuf.cs</c> renders raw bytes. The
/// per-(type, format) dispatch table picks the right one by writer.</para>
///
/// <para><c>Path</c> is a composed facet — an image MAY be backed by a file
/// (<c>image.Path.Exists</c> navigates the path through the typed-property
/// catalog), or it MAY be base64-decoded from memory (<c>Path = null</c>).
/// Routing key / serializer always stays <c>image</c>: no <c>path|image</c>
/// union. See plan/build-vs-runtime.md "composition, not union".</para>
/// </summary>
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>, global::app.data.IKindValidatable, global::app.data.IStrictKindEnforcer
{
    public static string Example => "/some/photo.jpg";
    public static string Shape => "string";

    // Null until loaded — a path-backed image reads nothing until first content
    // access. A bytes-backed image sets this in the constructor.
    private byte[]? _bytes;
    private string? _mime;

    // Imprinted strict-kind requirement (from `as image/<kind> strict`). When
    // set, the content must sniff to this kind the moment bytes are present —
    // checked at the set for an already-loaded image, or at BytesAsync for a
    // lazy path-backed one (which throws).
    private string? _requiredKind;

    /// <summary>
    /// The in-memory image bytes. For a path-backed image this is empty until
    /// <see cref="BytesAsync"/> has loaded the content (the lazy load is async
    /// because path reads pass through the actor permission gate — a sync getter
    /// must not block on I/O). Use <see cref="BytesAsync"/> to load-then-read.
    /// </summary>
    [global::app.Out, global::app.Store]
    public byte[] Bytes => _bytes ?? System.Array.Empty<byte>();

    [global::app.Out, global::app.Store]
    public string Mime => _mime ??= (Path?.MimeType ?? "application/octet-stream");

    /// <summary>
    /// The image renders itself, per wire format. The portable form is base64
    /// (json/plang/any). A text stream can't carry base64 readably — it emits
    /// the source location when wired, else a scannable label. A protobuf
    /// stream carries the raw bytes.
    /// </summary>
    public override void Write(global::app.channel.serializer.IWriter writer)
    {
        switch (writer.Format)
        {
            case "text":
                if (Path != null)
                {
                    try { writer.String(Path.Relative); return; }
                    catch (System.Exception ex) when (ex is not (System.OutOfMemoryException or System.StackOverflowException))
                    { /* fall through to bare label */ }
                }
                writer.String($"[image: {Mime} {Bytes.Length}B]");
                return;
            case "protobuf":
                writer.Bytes(Bytes);
                return;
            default:
                writer.String(System.Convert.ToBase64String(Bytes));
                return;
        }
    }

    /// <summary>An image's entity: kind is the format token from its own mime
    /// ("image/gif" → "gif"), canonicalised through the registry when reachable.</summary>
    protected internal override global::app.type.@this Type
    {
        get
        {
            var t = Path?.Context?.App.Format.TypeFromMime(Mime);
            var kind = t is { IsNull: false } ? t.Kind?.Name
                : Mime.IndexOf('/') is var slash and >= 0 ? Mime[(slash + 1)..] : null;
            return new global::app.type.@this("image") { Kind = (kind == "octet-stream" ? null : kind) is { } k ? new global::app.type.kind.@this(k) : null };
        }
    }

    /// <summary>
    /// Source path. Set for a path-backed image (content lazy-loads from here)
    /// or as provenance for a bytes-backed one (network fetch, base64 decode);
    /// null when the image is purely in-memory. Carries the path's typed
    /// properties (<c>Exists</c>, <c>Relative</c>, …) when present.
    /// </summary>
    [global::app.LlmBuilder, global::app.Out, global::app.Store]
    public global::app.type.item.path.@this? Path { get; init; }

    private int? _width;
    private int? _height;

    /// <summary>Lazy width — read on first access via SixLabors.ImageSharp.
    /// Valid once bytes are in memory (bytes-backed, or after <see cref="BytesAsync"/>).</summary>
    public int Width => _width ??= ProbeDimensions().w;

    /// <summary>Lazy height — read on first access via SixLabors.ImageSharp.</summary>
    public int Height => _height ??= ProbeDimensions().h;

    /// <summary>Bytes-backed: the content is already in hand (network fetch, base64 decode).</summary>
    /// <summary>THE PURE CORE (context-free part) — an <c>image</c> passes through; a <c>byte[]</c>
    /// declared <c>as image</c> becomes the image its magic bytes name (the declaration is the ask).
    /// A string source needs the scheme registry (a context) and lives in the courier below; anything
    /// else declines (<c>null</c>).</summary>
    public static @this? Create(object? raw)
    {
        if (raw is @this self) return self;
        object? value = raw is global::app.type.item.@this rit ? rit.Clr<object>() : raw;
        return value is byte[] bytes ? FromBytes(bytes) : null;
    }

    /// <summary>The ICreate courier face — pass-through / byte[] via the core; a string builds a
    /// scheme-path image via <c>Scheme.From</c> (uses <c>data.Context</c>). A non-string source
    /// declines silently; an unregistered/failed scheme lands the reason on <paramref name="data"/>.</summary>
    public static @this? Create(object? value, global::app.data.@this data)
    {
        if (Create(value) is { } built) return built;
        if (((value as global::app.type.item.@this)?.Clr<object>() ?? value) is not string raw) return null;
        try { return new @this(data.Context.App.Type.Scheme.From(raw, data.Context)); }
        catch (global::app.type.item.path.scheme.SchemeNotRegistered snr)
        {
            data.Fail(new global::app.error.Error(snr.Message, "SchemeNotRegistered", 400)
                { FixSuggestion = $"Register a factory for scheme '{snr.Scheme}', or use a bare/file:// path." });
            return null;
        }
        catch (System.Exception ex) when (ex is not (System.NullReferenceException or System.OutOfMemoryException or System.StackOverflowException))
        {
            data.Fail(new global::app.error.Error(ex.InnerException?.Message ?? ex.Message, "PathHandleConstructionFailed", 400));
            return null;
        }
    }

    public @this(byte[] bytes, string mime, global::app.type.item.path.@this? path = null)
    {
        _bytes = bytes ?? System.Array.Empty<byte>();
        _mime = mime ?? "application/octet-stream";
        Path = path;
        if (path != null) Accumulate(path);   // born from a path → `is path` from the type history
    }

    /// <summary>
    /// Bytes-backed but path-aware: content already read, the SAME source path
    /// object retained (Mime derives from it). file.read's image lift uses this
    /// so the value carries its path without decomposing it into loose
    /// primitives (bytes + mime + a re-resolved path).
    /// </summary>
    public @this(byte[] bytes, global::app.type.item.path.@this path)
    {
        _bytes = bytes ?? System.Array.Empty<byte>();
        Path = path;
        if (path != null) Accumulate(path);   // born from a path → `is path` from the type history
    }

    /// <summary>
    /// Path-backed: a lazy handle. <c>.Path</c> is set and <b>nothing is read</b>
    /// — the content materializes from the path on the first
    /// <see cref="BytesAsync"/>. The proving instance for reference-fundamental
    /// laziness (audio/video follow the same shape).
    /// </summary>
    public @this(global::app.type.item.path.@this path)
    {
        Path = path ?? throw new System.ArgumentNullException(nameof(path));
        Accumulate(path);   // born from a path → `is path` from the type history
    }

    /// <summary>Imprint the strict kind this image's content must match (from `as image/<kind> strict`).</summary>
    public void RequireStrictKind(string kind) => _requiredKind = kind;

    /// <summary>
    /// Materialize door — load the path-backed image's bytes into memory through the
    /// path's auth gate (once, cached in <c>_bytes</c>) and run the strict-kind check.
    /// A bytes-backed image is already loaded. <c>.Value()</c> is the uniform
    /// materialization for every reference fundamental, which is why the serializer
    /// needs no separate load pass; the sync <c>Bytes</c> getter then serves the cached
    /// bytes the leaf write emits. Failures (IO, strict mismatch) land on the data
    /// binding, answer absent.
    /// </summary>
    public override async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Value(global::app.data.@this data)
    {
        if (_bytes == null && Path != null)
        {
            var read = await Path.ReadBytes();
            // The path's read error rides through WHOLE — its key, message and inner
            // exception — instead of being flattened into a bare-string IOException.
            if (!read.Success)
            {
                data.Fail(read.Error ?? new global::app.error.Error(
                    $"could not read image from '{Path}'.", "ImageReadFailed", 400));
                return Absent;
            }
            _bytes = (await read.Value())?.Value ?? System.Array.Empty<byte>();
            // Strict kind fires here, at byte-materialization — the set stayed lazy.
            if (CheckStrictKind() is { ok: false } mismatch)
            {
                data.Fail(new global::app.error.Error(
                    $"Strict kind mismatch: declared kind '{_requiredKind}'"
                    + (mismatch.actualKind != null ? $" but content is '{mismatch.actualKind}'." : "."),
                    "StrictKindMismatch", 400));
                return Absent;
            }
        }
        else if (_bytes == null)
        {
            _bytes = System.Array.Empty<byte>();
        }
        return this;
    }

    /// <summary>
    /// Sniff the loaded bytes against the imprinted kind. Null when no strict
    /// kind was required or the bytes are not loaded yet (a lazy path-backed
    /// image defers enforcement to <see cref="BytesAsync"/>).
    /// </summary>
    public (bool ok, string? actualKind)? CheckStrictKind()
    {
        if (_requiredKind == null || _bytes == null || _bytes.Length == 0) return null;
        return ValidateKind(_bytes, _requiredKind);
    }

    public override async System.Threading.Tasks.Task<bool> AsBooleanAsync()
    {
        // Truthiness without forcing a full load: in-memory bytes are truthy
        // when non-empty; a path-backed image is truthy when its resource exists
        // (existence probe, not a byte read — keeps the handle lazy).
        if (_bytes != null) return _bytes.Length > 0;
        if (Path != null)
        {
            var exists = await Path.ExistsAsync();
            return exists.Success && await exists.ToBooleanAsync();
        }
        return false;
    }

    /// <summary>
    /// Sniffs the magic bytes of <paramref name="value"/> (a <c>byte[]</c>) via
    /// ImageSharp's <c>DetectFormat</c>, and compares the format's primary
    /// extension to <paramref name="requiredKind"/>. Returns <c>(true, null)</c>
    /// on match, <c>(false, actualKind)</c> on mismatch.
    /// </summary>
    public (bool ok, string? actualKind) ValidateKind(object value, string requiredKind)
    {
        // Sniff the realistic value shapes: raw byte[], or a loaded image
        // instance (read-lift) — read its own bytes, not the probe's empty ones.
        var bytes = value switch
        {
            byte[] b => b,
            @this img => img.Bytes,
            _ => Bytes
        };
        if (bytes == null || bytes.Length == 0) return (false, null);
        try
        {
            var fmt = SixLabors.ImageSharp.Image.DetectFormat(bytes);
            if (fmt == null) return (false, null);
            // ImageSharp's IImageFormat exposes FileExtensions (e.g. ["gif"],
            // ["jpg","jpeg"]); the canonical form for "kind" is the shortest.
            string? actual = null;
            foreach (var ext in fmt.FileExtensions)
            {
                if (actual == null || ext.Length < actual.Length) actual = ext;
            }
            if (actual == null) return (false, null);
            if (string.Equals(actual, requiredKind, System.StringComparison.OrdinalIgnoreCase))
                return (true, null);
            return (false, actual);
        }
        catch (System.Exception ex) when (ex is not (System.OutOfMemoryException or System.StackOverflowException))
        {
            return (false, null);
        }
    }

    private (int w, int h) ProbeDimensions()
    {
        if (Bytes.Length == 0) return (0, 0);
        try
        {
            var info = SixLabors.ImageSharp.Image.Identify(Bytes);
            if (info != null) return (info.Width, info.Height);
        }
        catch (System.Exception ex) when (ex is not (System.OutOfMemoryException or System.StackOverflowException))
        { /* probe failure → (0,0); never throw */ }
        return (0, 0);
    }
}
