namespace App.modules.http;

/// <summary>
/// HTTP method for requests. Maps from PLang step text via LLM builder.
/// </summary>
public enum HttpMethod
{
    GET, POST, PUT, DELETE, PATCH, HEAD, OPTIONS, QUERY
}

/// <summary>
/// Stream format for OnStream callbacks.
/// </summary>
public enum StreamFormat
{
    /// <summary>Newline-delimited lines (covers NDJSON, OpenAI-style, most streaming APIs).</summary>
    Line,
    /// <summary>Server-Sent Events (data: fields, \n\n boundaries).</summary>
    SSE,
    /// <summary>Raw byte chunks as they arrive from transport.</summary>
    Bytes
}

/// <summary>
/// Explicit content hint for upload action. Null = auto-detect.
/// </summary>
public enum ContentAs
{
    File, Base64, Form, Text
}

/// <summary>
/// File existence handling for download action.
/// </summary>
public enum FileExists
{
    /// <summary>Return error if file exists (default).</summary>
    Error,
    /// <summary>Overwrite existing file.</summary>
    Overwrite,
    /// <summary>Skip download, return path silently.</summary>
    Skip
}

/// <summary>
/// Progress data for download/upload OnProgress callbacks.
/// </summary>
public record TransferProgress
{
    public long BytesTransferred { get; init; }
    public long? TotalBytes { get; init; }
    public double? Percentage { get; init; }
}
