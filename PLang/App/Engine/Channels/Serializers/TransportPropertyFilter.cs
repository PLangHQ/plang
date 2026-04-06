using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace App.Engine.Channels.Serializers;

/// <summary>
/// Transport filter that overrides [JsonIgnore] for properties marked with [In] or [Out].
///
/// Normal JsonSerializer skips [JsonIgnore] properties. For application/plang transport,
/// we need Signature to round-trip. This filter re-includes ignored properties that have
/// the target transport attribute.
///
/// Usage:
///   TransportPropertyFilter.ForInbound  — re-includes [In] properties (deserialization)
///   TransportPropertyFilter.ForOutbound — re-includes [Out] properties (serialization)
/// </summary>
public static class TransportPropertyFilter
{
    /// <summary>
    /// Modifier that re-includes [In] properties that were excluded by [JsonIgnore].
    /// Use for deserializing inbound transport data (application/plang responses).
    /// </summary>
    public static void ForInbound(JsonTypeInfo typeInfo)
    {
        ReIncludeIgnored(typeInfo, typeof(InAttribute));
    }

    /// <summary>
    /// Modifier that re-includes [Out] properties that were excluded by [JsonIgnore].
    /// Use for serializing outbound transport data (application/plang responses).
    /// </summary>
    public static void ForOutbound(JsonTypeInfo typeInfo)
    {
        ReIncludeIgnored(typeInfo, typeof(OutAttribute));
    }

    private static void ReIncludeIgnored(JsonTypeInfo typeInfo, Type attributeType)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

        var clrType = typeInfo.Type;
        foreach (var prop in clrType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.IsDefined(attributeType, false)) continue;

            // Remove any existing entry — [JsonIgnore] may have left a hidden exclusion
            for (int i = typeInfo.Properties.Count - 1; i >= 0; i--)
            {
                if (typeInfo.Properties[i].Name.Equals(prop.Name, StringComparison.OrdinalIgnoreCase))
                    typeInfo.Properties.RemoveAt(i);
            }

            // Create a fresh property entry that bypasses [JsonIgnore]
            var jsonProp = typeInfo.CreateJsonPropertyInfo(prop.PropertyType, prop.Name.ToLowerInvariant());
            jsonProp.Get = prop.CanRead ? prop.GetValue : null;
            jsonProp.Set = prop.CanWrite ? prop.SetValue : null;
            typeInfo.Properties.Add(jsonProp);
        }
    }
}
