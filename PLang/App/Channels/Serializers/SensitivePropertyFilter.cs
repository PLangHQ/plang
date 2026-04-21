using System.Text.Json.Serialization.Metadata;

namespace App.Channels.Serializers;

/// <summary>
/// Modifiers that enforce <see cref="SensitiveAttribute"/> during JSON serialization.
///
/// Two modes for different output intents:
///  - <see cref="Strip"/> — removes the property entirely. Used on user-facing output
///    channels (<c>JsonStreamSerializer</c>, envelope) where the consumer should not
///    even learn that a secret exists.
///  - <see cref="Mask"/> — replaces the value with <c>"******"</c>, keeping the property
///    visible. Used on diagnostic output (test reports, debug dumps) where the reader
///    needs to see the shape of the data and an absent key would be ambiguous
///    (missing vs. null vs. redacted).
///
/// Storage paths (raw <see cref="System.Text.Json.JsonSerializer"/>, <c>PrWrite</c>) do not
/// apply either modifier — sensitive data persists so it can be re-read.
/// </summary>
public static class SensitivePropertyFilter
{
    private const string MaskValue = "******";

    /// <summary>Removes [Sensitive] properties from serialization output.</summary>
    public static void Strip(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

        for (int i = typeInfo.Properties.Count - 1; i >= 0; i--)
        {
            var prop = typeInfo.Properties[i];
            if (prop.AttributeProvider?.IsDefined(typeof(SensitiveAttribute), false) == true)
                typeInfo.Properties.RemoveAt(i);
        }
    }

    /// <summary>
    /// Replaces [Sensitive] string property values with <c>"******"</c> during
    /// serialization. For non-string [Sensitive] properties, falls back to stripping
    /// the property — the mask literal would not match the declared type.
    /// </summary>
    public static void Mask(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

        for (int i = typeInfo.Properties.Count - 1; i >= 0; i--)
        {
            var prop = typeInfo.Properties[i];
            if (prop.AttributeProvider?.IsDefined(typeof(SensitiveAttribute), false) != true)
                continue;

            if (prop.PropertyType == typeof(string))
                prop.Get = _ => MaskValue;
            else
                typeInfo.Properties.RemoveAt(i);
        }
    }
}
