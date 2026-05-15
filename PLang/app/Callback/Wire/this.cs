using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace app.Callback.Wire;

/// <summary>
/// Wire-format configuration for the callback subsystem. Holds the JsonSerializerOptions
/// used by both AskCallback and ErrorCallback for wire (de)serialization. JsonSerializerOptions
/// becomes cache-frozen on first use, so one app-scoped instance is the canonical reuse pattern.
/// The Filters.Sensitive modifier strips [Sensitive]-marked properties from the wire —
/// captured Variables can carry arbitrary objects whose typed properties may include secrets
/// (Security v1 S-F4).
///
/// Read as <c>app.Callback.Wire.Options</c>.
/// </summary>
public sealed class @this
{
    internal JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { app.Channels.Serializers.Filters.Sensitive.Strip }
        }
    };
}
