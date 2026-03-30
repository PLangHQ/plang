using System.Text.Json;

namespace PLang.Runtime2.Engine.Utility;

/// <summary>
/// Shared JSON serializer options.
/// Specialized options with custom converters (signing, transport) stay with their owners.
/// </summary>
public static class Json
{
    /// <summary>
    /// Case-insensitive property matching for deserialization.
    /// Use for: .pr files, app.pr, HTTP responses, any JSON read where casing may vary.
    /// </summary>
    public static readonly JsonSerializerOptions CaseInsensitiveRead = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// CamelCase + indented for all PLang JSON output.
    /// Use for: .pr files, app.pr, any JSON we write.
    /// </summary>
    public static readonly JsonSerializerOptions CamelCaseIndented = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
}
