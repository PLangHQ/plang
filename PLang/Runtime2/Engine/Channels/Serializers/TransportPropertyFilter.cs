using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace PLang.Runtime2.Engine.Channels.Serializers;

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

        // Check the actual CLR type for properties with our transport attribute
        // that are missing from typeInfo.Properties (because [JsonIgnore] excluded them)
        var clrType = typeInfo.Type;
        foreach (var prop in clrType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.IsDefined(attributeType, false)) continue;

            // Check if this property is already in the type info (not ignored)
            var alreadyPresent = false;
            foreach (var existing in typeInfo.Properties)
            {
                if (existing.Name.Equals(prop.Name, StringComparison.OrdinalIgnoreCase))
                {
                    alreadyPresent = true;
                    break;
                }
            }

            if (alreadyPresent) continue;

            // Re-add the ignored property
            var jsonProp = typeInfo.CreateJsonPropertyInfo(prop.PropertyType, prop.Name.ToLowerInvariant());
            jsonProp.Get = prop.CanRead ? prop.GetValue : null;
            jsonProp.Set = prop.CanWrite ? prop.SetValue : null;
            typeInfo.Properties.Add(jsonProp);
        }
    }
}
