using System.Text.Json;

namespace PLang.Runtime2.Engine.Utility;

/// <summary>
/// Shared JSON serializer options. Each field name describes its purpose.
/// Specialized options with custom converters (signing, transport) stay with their owners.
/// </summary>
public static class Json
{
    /// <summary>
    /// Read JSON with case-insensitive property matching.
    /// Use for: .pr files, app.pr, any JSON deserialization where casing may vary.
    /// </summary>
    public static readonly JsonSerializerOptions CaseInsensitiveRead = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Write JSON with camelCase naming and indentation.
    /// Use for: app.pr, general human-readable JSON output.
    /// </summary>
    public static readonly JsonSerializerOptions CamelCaseIndented = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Write .pr files — camelCase, indented, nulls included (deterministic).
    /// Every property is written so .pr files don't change when runtime defaults change.
    /// </summary>
    public static readonly JsonSerializerOptions PrFileWrite = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Compact JSON — no indentation, no special naming.
    /// Use for: inline JSON in LLM prompts, log output.
    /// </summary>
    public static readonly JsonSerializerOptions Compact = new()
    {
        WriteIndented = false
    };
}
