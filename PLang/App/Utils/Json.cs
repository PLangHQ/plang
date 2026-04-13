using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace App.Utils;

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
        Converters = { new JsonStringEnumConverter(allowIntegerValues: true), new EmptyStringToNullEnumConverterFactory() },
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
        if (ns == null || !ns.StartsWith("App.Goals", StringComparison.Ordinal))
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

/// <summary>
/// Handles empty strings for nullable enum properties during JSON deserialization.
/// LLMs produce "" for unset enum fields — this converts them to null instead of failing.
/// </summary>
public class EmptyStringToNullEnumConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        if (!typeToConvert.IsGenericType) return false;
        if (typeToConvert.GetGenericTypeDefinition() != typeof(Nullable<>)) return false;
        return Nullable.GetUnderlyingType(typeToConvert)?.IsEnum == true;
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var enumType = Nullable.GetUnderlyingType(typeToConvert)!;
        var converterType = typeof(EmptyStringToNullEnumConverter<>).MakeGenericType(enumType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

public class EmptyStringToNullEnumConverter<T> : JsonConverter<T?> where T : struct, Enum
{
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (string.IsNullOrEmpty(str)) return null;
            if (Enum.TryParse<T>(str, ignoreCase: true, out var result)) return result;
        }
        if (reader.TokenType == JsonTokenType.Number)
        {
            var num = reader.GetInt32();
            return (T)Enum.ToObject(typeof(T), num);
        }
        return null;
    }

    public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteStringValue(value.Value.ToString());
        else
            writer.WriteNullValue();
    }
}
