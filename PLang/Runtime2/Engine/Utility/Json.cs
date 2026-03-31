using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace PLang.Runtime2.Engine.Utility;

/// <summary>
/// Shared JSON serializer options.
/// Specialized options with custom converters (signing, transport) stay with their owners.
/// </summary>
public static class Json
{
    /// <summary>
    /// Case-insensitive property matching for deserialization.
    /// Handles enums as strings and string↔number coercion (LLM output is non-deterministic).
    /// Use for: .pr files, app.pr, HTTP responses, any JSON read where casing may vary.
    /// </summary>
    public static readonly JsonSerializerOptions CaseInsensitiveRead = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true) },
        NumberHandling = JsonNumberHandling.AllowReadingFromString
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

    /// <summary>
    /// .pr file serialization — only properties marked with [Store] are included.
    /// CamelCase, indented, nulls omitted.
    /// </summary>
    public static readonly JsonSerializerOptions PrWrite = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { StoreOnlyModifier }
        }
    };

    private static void StoreOnlyModifier(JsonTypeInfo typeInfo)
    {
        // Only filter properties on our own types (Goal, Step, Action, etc.)
        // Leave framework types (List, Dictionary, Data, etc.) alone
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
            return;

        var ns = typeInfo.Type.Namespace;
        if (ns == null || !ns.StartsWith("PLang.Runtime2.Engine.Goals", StringComparison.Ordinal))
            return;

        foreach (var prop in typeInfo.Properties)
        {
            if (prop.AttributeProvider == null)
                continue;

            var hasStore = prop.AttributeProvider
                .GetCustomAttributes(typeof(StoreAttribute), inherit: true)
                .Length > 0;

            if (!hasStore)
                prop.ShouldSerialize = (_, _) => false;
        }
    }
}
