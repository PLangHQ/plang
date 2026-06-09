namespace app.type.image;

/// <summary>
/// String → image and bytes → image. <c>Resolve(string)</c> handles three
/// forms: a file/http path (loaded as bytes), a <c>data:</c> URL (header
/// gives the MIME, payload is base64), or a bare base64 string (MIME sniffed
/// from magic bytes).
/// </summary>
public sealed partial class @this
{
    /// <summary>
    /// Synchronous string → image. Handles in-memory forms only: a
    /// <c>data:</c> URL or a bare base64 string. Path-shaped inputs require
    /// async file I/O and route through <see cref="ResolveAsync"/> (file.read
    /// in production); this synchronous overload returns null for them rather
    /// than blocking on async I/O.
    /// </summary>
    public static @this? Resolve(string raw, global::app.actor.context.@this context)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        raw = raw.Trim();

        if (raw.StartsWith("data:", System.StringComparison.OrdinalIgnoreCase))
            return FromDataUrl(raw);

        // Path-shaped — caller must use ResolveAsync. Don't sync-over-async
        // the path verbs; they are async by design (the http scheme needs it).
        if (raw.Contains('/') || raw.Contains('\\') || HasImageExtension(raw))
            return null;

        // Bare base64 — last resort.
        try
        {
            var bytes = System.Convert.FromBase64String(raw);
            var mime = SniffMime(bytes);
            return mime != null ? new @this(bytes, mime) : null;
        }
        catch (System.FormatException) { return null; }
    }

    /// <summary>
    /// Async string → image. Adds the file-path branch (any scheme the
    /// app's <see cref="app.type.path.@this"/> registry supports —
    /// <c>file://</c>, <c>http://</c>, …) on top of the in-memory forms
    /// <see cref="Resolve"/> handles.
    /// </summary>
    public static async System.Threading.Tasks.Task<@this?> ResolveAsync(string raw, global::app.actor.context.@this context)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        raw = raw.Trim();

        if (raw.StartsWith("data:", System.StringComparison.OrdinalIgnoreCase))
            return FromDataUrl(raw);

        if (raw.Contains('/') || raw.Contains('\\') || HasImageExtension(raw))
        {
            try
            {
                var p = global::app.type.path.@this.Resolve(raw, context);
                var exists = await p.ExistsAsync();
                if (!exists.Success || (await exists.Value())?.Value == false) return null;
                var read = await p.ReadBytes();
                if (!read.Success || read.Peek() == null) return null;
                var ext = p is global::app.type.path.file.@this fp ? fp.Extension : "";
                { var __rb = (byte[])(await read.Value())!; return new @this(__rb, SniffMime(__rb) ?? MimeFromExtension(ext), p); }
            }
            catch (System.Exception ex) when (ex is not (System.OutOfMemoryException or System.StackOverflowException))
            { return null; }
        }

        try
        {
            var bytes = System.Convert.FromBase64String(raw);
            var mime = SniffMime(bytes);
            return mime != null ? new @this(bytes, mime) : null;
        }
        catch (System.FormatException) { return null; }
    }

    /// <summary>
    /// Bytes → image. MIME is sniffed from the magic bytes. Named distinctly
    /// from <see cref="Resolve(string, global::app.actor.context.@this)"/>
    /// because catalog reflection (<c>GetMethod("Resolve", static)</c>)
    /// throws <see cref="System.Reflection.AmbiguousMatchException"/> when
    /// two same-name static overloads exist.
    /// </summary>
    public static @this? FromBytes(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0) return null;
        var mime = SniffMime(bytes);
        return mime != null ? new @this(bytes, mime) : null;
    }

    private static @this? FromDataUrl(string raw)
    {
        // data:image/png;base64,iVBORw0...
        var commaIdx = raw.IndexOf(',');
        if (commaIdx < 0) return null;
        var header = raw[5..commaIdx];
        var payload = raw[(commaIdx + 1)..];
        var semiIdx = header.IndexOf(';');
        var mime = semiIdx >= 0 ? header[..semiIdx] : header;
        if (string.IsNullOrEmpty(mime)) return null;
        try
        {
            var bytes = System.Convert.FromBase64String(payload);
            return new @this(bytes, mime);
        }
        catch (System.FormatException) { return null; }
    }

    internal static string? SniffMime(byte[] bytes)
    {
        if (bytes.Length < 4) return null;
        // PNG: 89 50 4E 47
        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return "image/png";
        // JPEG: FF D8 FF
        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return "image/jpeg";
        // GIF: GIF8
        if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38) return "image/gif";
        // WebP: RIFF ... WEBP
        if (bytes.Length >= 12 && bytes[0] == 'R' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == 'F'
            && bytes[8] == 'W' && bytes[9] == 'E' && bytes[10] == 'B' && bytes[11] == 'P') return "image/webp";
        // BMP: BM
        if (bytes[0] == 0x42 && bytes[1] == 0x4D) return "image/bmp";
        return null;
    }

    internal static string MimeFromExtension(string ext)
    {
        ext = ext.TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "jpg" or "jpeg" => "image/jpeg",
            "png" => "image/png",
            "gif" => "image/gif",
            "webp" => "image/webp",
            "bmp" => "image/bmp",
            "svg" => "image/svg+xml",
            _ => "application/octet-stream",
        };
    }

    private static bool HasImageExtension(string raw)
    {
        var dotIdx = raw.LastIndexOf('.');
        if (dotIdx < 0 || dotIdx >= raw.Length - 1) return false;
        var ext = raw[(dotIdx + 1)..].ToLowerInvariant();
        return ext is "jpg" or "jpeg" or "png" or "gif" or "webp" or "bmp" or "svg";
    }
}
