namespace App.Utils;

/// <summary>
/// Extension ↔ MIME type ↔ CLR type mapping, split out of TypeMapping so the
/// concerns don't tangle. Pure static table.
/// </summary>
public static class MimeTypes
{
    /// <summary>Gets the MIME type for a file extension (with or without the leading dot).</summary>
    public static string GetMimeType(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return "application/octet-stream";

        return extension.ToLowerInvariant().TrimStart('.') switch
        {
            "md" => "text/markdown",
            "json" => "application/json",
            "xml" => "text/xml",
            "html" or "htm" => "text/html",
            "css" => "text/css",
            "js" => "text/javascript",
            "csv" => "text/csv",
            "yaml" or "yml" => "text/yaml",
            "txt" => "text/plain",
            "llm" => "text/plain",
            "goal" => "text/plain",
            "pr" => "application/plang-goal",
            "png" => "image/png",
            "jpg" or "jpeg" => "image/jpeg",
            "gif" => "image/gif",
            "svg" => "image/svg+xml",
            "webp" => "image/webp",
            "mp3" => "audio/mpeg",
            "wav" => "audio/wav",
            "mp4" => "video/mp4",
            "pdf" => "application/pdf",
            "zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    /// Resolves a MIME type to a CLR type for deserialization. Returns null when the
    /// input isn't a MIME string (no slash) or isn't a recognized family.
    /// </summary>
    public static System.Type? TryGetClrType(string mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType) || !mimeType.Contains('/')) return null;

        if (mimeType.StartsWith("text/", System.StringComparison.OrdinalIgnoreCase))
            return typeof(string);
        if (mimeType.StartsWith("image/", System.StringComparison.OrdinalIgnoreCase)
            || mimeType.StartsWith("audio/", System.StringComparison.OrdinalIgnoreCase)
            || mimeType.StartsWith("video/", System.StringComparison.OrdinalIgnoreCase))
            return typeof(byte[]);
        if (mimeType.Equals("application/json", System.StringComparison.OrdinalIgnoreCase))
            return typeof(object);
        if (mimeType.Equals("application/plang-goal", System.StringComparison.OrdinalIgnoreCase))
            return typeof(App.Goals.Goal.@this);
        if (mimeType.Equals("application/octet-stream", System.StringComparison.OrdinalIgnoreCase))
            return typeof(byte[]);

        return null;
    }
}
