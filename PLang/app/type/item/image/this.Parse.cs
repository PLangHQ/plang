namespace app.type.item.image;

/// <summary>
/// Bytes → image, with MIME sniffed from the magic bytes. The string forms
/// (data-url, bare base64, path) are gone — a data-url is a <c>base64</c> value
/// (its bytes reach image via the <c>Create</c> base64 arm), and a file path is
/// loaded through the path verbs; image itself only ever meets bytes.
/// </summary>
public sealed partial class @this
{
    /// <summary>
    /// Bytes → image. MIME is sniffed from the magic bytes; the image is born holding the app's type of that
    /// kind (<c>image&lt;png&gt;</c>), asked of <paramref name="context"/>'s types. Named distinctly
    /// from <see cref="Create(object?, global::app.actor.context.@this)"/> because catalog reflection
    /// (<c>GetMethod("Create", static)</c>) can't disambiguate same-name static
    /// overloads — a documented exception, not a pattern.
    /// </summary>
    public static @this? FromBytes(byte[] bytes, global::app.actor.context.@this context)
    {
        if (bytes == null || bytes.Length == 0) return null;
        return Sniff(bytes) is { } kind
            ? new @this(bytes, context.App.type.list[new global::app.type.@this("image", kind), context])
            : null;
    }

    /// <summary>The canonical kind name the magic bytes name — the image looks it up in its own kinds, and
    /// the mime is asked of the kind.</summary>
    private static string? Sniff(byte[] bytes)
    {
        if (bytes.Length < 4) return null;
        // PNG: 89 50 4E 47
        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return "png";
        // JPEG: FF D8 FF
        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return "jpg";
        // GIF: GIF8
        if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38) return "gif";
        // WebP: RIFF ... WEBP
        if (bytes.Length >= 12 && bytes[0] == 'R' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == 'F'
            && bytes[8] == 'W' && bytes[9] == 'E' && bytes[10] == 'B' && bytes[11] == 'P') return "webp";
        // BMP: BM
        if (bytes[0] == 0x42 && bytes[1] == 0x4D) return "bmp";
        return null;
    }
}
