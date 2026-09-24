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
    /// Bytes → image. MIME is sniffed from the magic bytes. Named distinctly
    /// from <see cref="Create(object?)"/> because catalog reflection
    /// (<c>GetMethod("Create", static)</c>) can't disambiguate same-name static
    /// overloads — a documented exception, not a pattern.
    /// </summary>
    public static @this? FromBytes(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0) return null;
        return Sniff(bytes) is { } found ? new @this(bytes, found.Mime, found.Kind) : null;
    }

    /// <summary>The mime and canonical kind the magic bytes name.</summary>
    private static (string Mime, string Kind)? Sniff(byte[] bytes)
    {
        if (bytes.Length < 4) return null;
        // PNG: 89 50 4E 47
        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return ("image/png", "png");
        // JPEG: FF D8 FF
        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return ("image/jpeg", "jpg");
        // GIF: GIF8
        if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38) return ("image/gif", "gif");
        // WebP: RIFF ... WEBP
        if (bytes.Length >= 12 && bytes[0] == 'R' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == 'F'
            && bytes[8] == 'W' && bytes[9] == 'E' && bytes[10] == 'B' && bytes[11] == 'P') return ("image/webp", "webp");
        // BMP: BM
        if (bytes[0] == 0x42 && bytes[1] == 0x4D) return ("image/bmp", "bmp");
        return null;
    }
}
