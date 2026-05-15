using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace app.data;

/// <summary>
/// Extension methods for JSON conversion. OBP: the string owns its conversion.
/// Relocated from <c>App.Utils.Json</c> in stage 27 — pure parsing utility, sits next to
/// the JSON-related Data machinery (TString, etc.).
/// </summary>
public static class JsonString
{
    /// <summary>
    /// Parses the string as JSON. If parsing fails, attempts to fix common issues
    /// (unescaped newlines/tabs in string values) and retries. Returns error if both fail.
    /// </summary>
    public static (JsonNode? result, Errors.IError? error) ToJson(this string str)
    {
        try { return (JsonNode.Parse(str), null); }
        catch (JsonException) { }

        try
        {
            var fixedJson = FixJsonStringValues(str);
            return (JsonNode.Parse(fixedJson), null);
        }
        catch (JsonException ex)
        {
            return (null, new Errors.ActionError(
                $"Invalid JSON: {ex.Message}", "JsonParseError", 400) { Exception = ex });
        }
    }

    /// <summary>
    /// Escapes unescaped control characters inside JSON string values.
    /// Walks the string tracking quote boundaries; replaces raw \n \r \t with escape sequences.
    /// </summary>
    internal static string FixJsonStringValues(string json)
    {
        var sb = new StringBuilder(json.Length);
        bool inString = false;
        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];
            if (c == '"' && (i == 0 || json[i - 1] != '\\'))
            {
                inString = !inString;
                sb.Append(c);
            }
            else if (inString && c == '\n') sb.Append("\\n");
            else if (inString && c == '\r') sb.Append("\\r");
            else if (inString && c == '\t') sb.Append("\\t");
            else sb.Append(c);
        }
        return sb.ToString();
    }
}

/// <summary>
/// Handles empty strings for nullable enum properties during JSON deserialization.
/// LLMs produce "" for unset enum fields — this converts them to null instead of failing.
/// Used by <see cref="app.Types.@this"/>'s case-insensitive read options and by HTTP transport.
/// </summary>
public class EmptyStringToNullEnumConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(System.Type typeToConvert)
    {
        if (!typeToConvert.IsGenericType) return false;
        if (typeToConvert.GetGenericTypeDefinition() != typeof(System.Nullable<>)) return false;
        return System.Nullable.GetUnderlyingType(typeToConvert)?.IsEnum == true;
    }

    public override JsonConverter CreateConverter(System.Type typeToConvert, JsonSerializerOptions options)
    {
        var enumType = System.Nullable.GetUnderlyingType(typeToConvert)!;
        var converterType = typeof(EmptyStringToNullEnumConverter<>).MakeGenericType(enumType);
        return (JsonConverter)System.Activator.CreateInstance(converterType)!;
    }
}

public class EmptyStringToNullEnumConverter<T> : JsonConverter<T?> where T : struct, System.Enum
{
    public override T? Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (string.IsNullOrEmpty(str)) return null;
            if (System.Enum.TryParse<T>(str, ignoreCase: true, out var result)) return result;
        }
        if (reader.TokenType == JsonTokenType.Number)
        {
            var num = reader.GetInt32();
            return (T)System.Enum.ToObject(typeof(T), num);
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
