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

    [global::app.Out, global::app.Store]
    public byte[] Bytes { get; }

    [global::app.Out, global::app.Store]
    public string Mime { get; }

    /// <summary>
    /// Optional source path. Null when the image was constructed from raw bytes
    /// (e.g. base64 decode, network fetch). Carries the path's typed properties
    /// (<c>Exists</c>, <c>Relative</c>, …) when present.
    /// </summary>
    [global::app.LlmBuilder, global::app.Out, global::app.Store]
    public global::app.type.path.@this? Path { get; init; }

    private int? _width;
    private int? _height;

    /// <summary>Lazy width — read on first access via SixLabors.ImageSharp.</summary>
    public int Width => _width ??= ProbeDimensions().w;

    /// <summary>Lazy height — read on first access via SixLabors.ImageSharp.</summary>
    public int Height => _height ??= ProbeDimensions().h;

    public @this(byte[] bytes, string mime, global::app.type.path.@this? path = null)
    {
        Bytes = bytes ?? System.Array.Empty<byte>();
        Mime = mime ?? "application/octet-stream";
        Path = path;
    }

    public System.Threading.Tasks.Task<bool> AsBooleanAsync()
        => System.Threading.Tasks.Task.FromResult(Bytes.Length > 0);

    /// <summary>
    /// Stage 4 fills in the magic-byte sniff comparing <paramref name="requiredKind"/>
    /// (e.g. "gif", "png") to the actual format derived from <see cref="Bytes"/>
    /// (via ImageSharp's <c>DetectFormat</c>). Stage 1 lands the marker only so
    /// the strict pipeline has a seam to call into.
    /// </summary>
    public (bool ok, string? actualKind) ValidateKind(object value, string requiredKind)
        => (true, null);

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
