using System.Text.Json.Serialization.Metadata;

namespace App.Channels.Serializers.Filters;

/// <summary>
/// Modifiers that enforce <see cref="SensitiveAttribute"/> during JSON serialization.
///
/// Two modes for different output intents:
///  - <see cref="Strip"/> — removes the property entirely. Used on user-facing output
///    channels (<c>global::App.Channels.Serializers.Serializer.Json</c>, envelope) where the consumer should not
///    even learn that a secret exists.
///  - <see cref="Mask"/> — replaces the value with <c>"******"</c>, keeping the property
///    visible. Used on diagnostic output (test reports, debug dumps) where the reader
///    needs to see the shape of the data and an absent key would be ambiguous
///    (missing vs. null vs. redacted).
///
/// Storage paths (raw <see cref="System.Text.Json.JsonSerializer"/>, <c>PrWrite</c>) do not
/// apply either modifier — sensitive data persists so it can be re-read.
/// </summary>
public static class Sensitive
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
    /// Replaces [Sensitive] property values with <c>"******"</c> during serialization.
    /// String properties are rewritten in place; non-string properties are replaced
    /// with a synthesized string-typed JsonPropertyInfo of the same name — the key
    /// stays visible (per <c>DiagnosticOutput</c>'s contract: absent vs null vs
    /// redacted must be distinguishable) while the value renders as <c>"******"</c>
    /// regardless of the source type (<c>byte[]</c>, record, etc.).
    /// </summary>
    public static void Mask(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

        for (int i = 0; i < typeInfo.Properties.Count; i++)
        {
            var prop = typeInfo.Properties[i];
            if (prop.AttributeProvider?.IsDefined(typeof(SensitiveAttribute), false) != true)
                continue;

            if (prop.PropertyType == typeof(string))
            {
                prop.Get = _ => MaskValue;
                continue;
            }

            var replacement = typeInfo.CreateJsonPropertyInfo(typeof(string), prop.Name);
            replacement.Get = _ => MaskValue;
            typeInfo.Properties[i] = replacement;
        }
    }
}
