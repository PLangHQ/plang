namespace app.http.response;

/// <summary>
/// Strongly-typed result returned by <c>http.request</c> and <c>http.upload</c>.
/// The <see cref="Body"/> is dispatched by the response Content-Type:
/// <c>application/json</c> → <c>JsonNode</c> / <c>Dictionary</c>;
/// <c>text/*</c> → <c>string</c>;
/// <c>image/*</c> / binary / missing Content-Type → <c>byte[]</c>.
/// </summary>
public sealed record @this(
    [property: Out] int Status,
    [property: Out] Dictionary<string, string> Headers,
    [property: Out] object? Body,
    System.TimeSpan Duration);
