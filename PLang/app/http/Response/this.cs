namespace app.http.Response;

/// <summary>
/// Strongly-typed result returned by <c>http.request</c> and <c>http.upload</c>.
/// The <see cref="Body"/> is dispatched by the response Content-Type:
/// <c>application/json</c> → <c>JsonNode</c> / <c>Dictionary</c>;
/// <c>text/*</c> → <c>string</c>;
/// <c>image/*</c> / binary / missing Content-Type → <c>byte[]</c>.
/// </summary>
public sealed record @this(
    int Status,
    Dictionary<string, string> Headers,
    object? Body,
    System.TimeSpan Duration);
