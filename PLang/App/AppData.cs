using System.Text.Json.Serialization;

namespace App;

/// <summary>
/// Represents the app.pr file data for a PLang application.
/// </summary>
public sealed class AppData
{
    /// <summary>
    /// Unique identifier (GUID) for the application.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>
    /// When the app was created.
    /// </summary>
    [JsonPropertyName("created")]
    public DateTime Created { get; set; }

    /// <summary>
    /// When the app was last updated.
    /// </summary>
    [JsonPropertyName("updated")]
    public DateTime Updated { get; set; }

    /// <summary>
    /// Name of the application.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Version of the builder used.
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; }
}
