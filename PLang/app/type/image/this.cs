namespace app.type.image;

/// <summary>
/// PLang <c>image</c> value — a binary blob plus its MIME type, optionally
/// backed by a source <see cref="app.type.path.@this"/>.
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
public sealed partial class @this : global::app.data.IBooleanResolvable, global::app.data.IKindValidatable
{
    public static string Example => "/some/photo.jpg";
    public static string Shape => "string";

    // Null until loaded — a path-backed image reads nothing until first content
    // access. A bytes-backed image sets this in the constructor.
    private byte[]? _bytes;
    private string? _mime;

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
    /// Source path. Set for a path-backed image (content lazy-loads from here)
    /// or as provenance for a bytes-backed one (network fetch, base64 decode);
    /// null when the image is purely in-memory. Carries the path's typed
    /// properties (<c>Exists</c>, <c>Relative</c>, …) when present.
    /// </summary>
    [global::app.LlmBuilder, global::app.Out, global::app.Store]
    public global::app.type.path.@this? Path { get; init; }

    private int? _width;
    private int? _height;

    /// <summary>Lazy width — read on first access via SixLabors.ImageSharp.
    /// Valid once bytes are in memory (bytes-backed, or after <see cref="BytesAsync"/>).</summary>
    public int Width => _width ??= ProbeDimensions().w;

    /// <summary>Lazy height — read on first access via SixLabors.ImageSharp.</summary>
    public int Height => _height ??= ProbeDimensions().h;

    /// <summary>Bytes-backed: the content is already in hand (network fetch, base64 decode).</summary>
    public @this(byte[] bytes, string mime, global::app.type.path.@this? path = null)
    {
        _bytes = bytes ?? System.Array.Empty<byte>();
        _mime = mime ?? "application/octet-stream";
        Path = path;
    }

    /// <summary>
    /// Path-backed: a lazy handle. <c>.Path</c> is set and <b>nothing is read</b>
    /// — the content materializes from the path on the first
    /// <see cref="BytesAsync"/>. The proving instance for reference-fundamental
    /// laziness (audio/video follow the same shape).
    /// </summary>
    public @this(global::app.type.path.@this path)
    {
        Path = path ?? throw new System.ArgumentNullException(nameof(path));
    }

    /// <summary>
    /// The image bytes, loaded through the path's auth gate on first access and
    /// cached. A bytes-backed image returns its in-memory bytes with no I/O; a
    /// path-backed image reads <see cref="Path"/> exactly once (subsequent calls
    /// return the cache). Async because the read goes through
    /// <c>FilePath.AuthGate</c> — never a blocking read in a sync getter. A read
    /// failure (missing file, denied) surfaces here, at first content access.
    /// </summary>
    public async System.Threading.Tasks.Task<byte[]> BytesAsync()
    {
        if (_bytes != null) return _bytes;
        if (Path == null) return _bytes = System.Array.Empty<byte>();
        var read = await Path.ReadBytes();
        if (!read.Success)
            throw new System.IO.IOException(read.Error?.Message ?? $"Could not read image from '{Path}'.");
        return _bytes = read.Value ?? System.Array.Empty<byte>();
    }

    public async System.Threading.Tasks.Task<bool> AsBooleanAsync()
    {
        // Truthiness without forcing a full load: in-memory bytes are truthy
        // when non-empty; a path-backed image is truthy when its resource exists
        // (existence probe, not a byte read — keeps the handle lazy).
        if (_bytes != null) return _bytes.Length > 0;
        if (Path != null)
        {
            var exists = await Path.ExistsAsync();
            return exists.Success && exists.Value;
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
        var bytes = value as byte[] ?? Bytes;
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
