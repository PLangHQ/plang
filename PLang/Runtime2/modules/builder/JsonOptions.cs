using System.Text.Json;
using System.Text.Json.Serialization;

namespace PLang.Runtime2.modules.builder;

/// <summary>
/// Shared JSON serializer options for builder module.
/// </summary>
public static class JsonOptions
{
    /// <summary>
    /// For reading .pr files — case-insensitive property matching.
    /// </summary>
    public static readonly JsonSerializerOptions CaseInsensitive = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// For writing .pr files — camelCase, include nulls (deterministic), indented.
    /// </summary>
    public static readonly JsonSerializerOptions PrFile = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// For writing app.pr — camelCase, indented.
    /// </summary>
    public static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
}
