namespace PLang.Services.OutputStream;

public static class PlangContentTypes
{
    // PLang formats (streaming, structured)
    public const string Ndjson = "plang/ndjson";
    public const string Json = "plang/json";
    public const string Binary = "plang/octet-stream";

    // Standard formats (for non-PLang consumers)
    public const string Text = "text/plain";
    public const string Html = "text/html";
    public const string JsonStandard = "application/json";

    /// <summary>
    /// Returns true if the content type is a PLang-specific format
    /// </summary>
    public static bool IsPlangFormat(string contentType)
    {
        return contentType.StartsWith("plang/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Maps Accept header values to PLang content types
    /// </summary>
    public static string FromAcceptHeader(string? acceptHeader, string defaultType = Text)
    {
        if (string.IsNullOrWhiteSpace(acceptHeader))
            return defaultType;

        var accept = acceptHeader.ToLowerInvariant();

        if (accept.StartsWith("plang/"))
            return accept;
        if (accept.StartsWith("application/plang"))
            return Ndjson; // Legacy support
        if (accept.StartsWith("application/json"))
            return JsonStandard;
        if (accept.StartsWith("text/html"))
            return Html;
        if (accept.StartsWith("text/plain"))
            return Text;

        return defaultType;
    }
}
