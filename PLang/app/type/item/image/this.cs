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

    // The canonical kind ("png", "jpg", …) — a fact named at birth: from the creator's
    // format registry for a path-born image, from the magic bytes or the wire otherwise.
    private readonly global::app.type.kind.@this? _kind;

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

    /// <summary>image's byte face — its loaded bytes, null until a path-backed image
    /// materializes at its Value door (the encode door loads first, which is right).</summary>
    public override byte[]? RawBytes => _bytes;

    /// <summary>The image's mime — a fact set at birth (bytes with a known mime, or a path's
    /// extension read through the creator's format registry) or at first load.</summary>
    [global::app.Out, global::app.Store]
    public string Mime => _mime ?? "application/octet-stream";

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
                // The source location as typed; a pure in-memory image shows a scannable label.
                writer.String(Path != null ? Path.ToString() : $"[image: {Mime} {Bytes.Length}B]");
                return;
            case "protobuf":
                writer.Bytes(Bytes);
                return;
            default:
                writer.String(System.Convert.ToBase64String(Bytes));
                return;
        }
    }

    /// <summary>An image's entity: name "image", kind = the canonical kind named at birth.</summary>
    protected internal override global::app.type.@this Type =>
        new global::app.type.@this("image") { Kind = _kind };

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
        // A base64 → its DECODED bytes → an image (mime sniffed off the magic bytes). Before the
        // unwrap below: lowering a base64 to its payload STRING would then die in the scheme registry.
        if (raw is global::app.type.item.base64.@this b64)
            return b64.RawBytes is { } bts ? FromBytes(bts) : null;
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
        try
        {
            var path = data.Context.App.Type.Scheme.From(raw, data.Context);
            return new @this(path, data.Context);
        }
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

    /// <summary>Bytes-backed, no source: the content is in hand (base64 decode, the wire).
    /// <paramref name="kind"/> is a fact the creator already has — sniffed off the magic
    /// bytes or read off the wire; null when unknown.</summary>
    public @this(byte[] bytes, string mime, string? kind = null)
    {
        _bytes = bytes ?? System.Array.Empty<byte>();
        _mime = mime ?? "application/octet-stream";
        _kind = kind is { Length: > 0 } ? new global::app.type.kind.@this(kind) : null;
    }

    /// <summary>
    /// Path-backed: a lazy handle. <c>.Path</c> is set and <b>nothing is read</b>
    /// — the content materializes from the path on the first <see cref="Value"/>.
    /// The proving instance for reference-fundamental laziness (audio/video follow
    /// the same shape). <paramref name="context"/> is the creator's, used once to name
    /// the mime and the kind from the path's extension, and not kept.
    /// </summary>
    public @this(global::app.type.item.path.@this path, global::app.actor.context.@this context)
    {
        Path = path ?? throw new System.ArgumentNullException(nameof(path));
        _mime = path.MimeType(context);
        _kind = path.Kind(context) is { IsNull: false } t ? t.Kind : null;
        this.list.Add(path);   // born from a path → `is path` from the type history
    }

    /// <summary>Path-backed with the content already read (the file read keeps images eager).</summary>
    public @this(byte[] bytes, global::app.type.item.path.@this path, global::app.actor.context.@this context)
        : this(path, context)
        => _bytes = bytes ?? System.Array.Empty<byte>();

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
            var read = await Path.ReadBytes(data.Context);
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

    public override async System.Threading.Tasks.Task<bool> AsBooleanAsync(global::app.actor.context.@this context)
    {
        // Truthiness without forcing a full load: in-memory bytes are truthy
        // when non-empty; a path-backed image is truthy when its resource exists
        // (existence probe, not a byte read — keeps the handle lazy).
        if (_bytes != null) return _bytes.Length > 0;
        if (Path != null)
        {
            var exists = await Path.ExistsAsync(context);
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
