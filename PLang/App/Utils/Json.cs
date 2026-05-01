using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    /// Symmetric write+read for snapshot cloning (variable.set, list.add, Variables.Set
    /// dot-path). CamelCase keys to match the rest of the pipeline (.pr, traces, viewer);
    /// case-insensitive on read so the deserialize half is lenient. Not indented — internal
    /// data, never human-read on the hot path.
    /// </summary>
    public static readonly JsonSerializerOptions SnapshotClone = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Diagnostic output — state dumps for humans to debug (test reports, variable
    /// snapshots in assertion failures, --debug dumps). [Sensitive] string properties
    /// are masked as "******" so the shape of the data is preserved while secrets are
    /// redacted. Distinct from storage (keeps sensitive data) and user output
    /// (strips it entirely).
    /// </summary>
    public static readonly JsonSerializerOptions DiagnosticOutput = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { Channels.Serializers.SensitivePropertyFilter.Mask }
        }
    };

    /// <summary>
    /// Formats a value for inclusion in a human-readable diagnostic string (assertion
    /// failure messages, console test output). Scalars render directly; strings get
    /// quoted; anything else goes through <see cref="DiagnosticOutput"/> so that
    /// <see cref="SensitiveAttribute"/> properties are masked. Never falls back to
    /// <c>value.ToString()</c> on arbitrary objects — that is the leak vector that
    /// bypasses the mask (e.g. a C# record's auto-generated ToString prints every
    /// field, [Sensitive] or not).
    /// </summary>
    public static string FormatForDiagnostic(object? value)
    {
        if (value == null) return "(null)";
        if (value is string s) return $"\"{s}\"";
        var type = value.GetType();
        if (type.IsPrimitive || value is decimal || value is DateTime
            || value is DateTimeOffset || value is TimeSpan || value is Guid
            || value is Enum)
            return value.ToString() ?? "(null)";
        try { return JsonSerializer.Serialize(value, DiagnosticOutput); }
        catch { return type.Name; }
    }

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
/// Extension methods for JSON conversion. OBP: the string owns its conversion.
/// </summary>
public static class JsonExtensions
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
            var fixedJson = Json.FixJsonStringValues(str);
            return (JsonNode.Parse(fixedJson), null);
        }
        catch (JsonException ex)
        {
            return (null, new Errors.ActionError(
                $"Invalid JSON: {ex.Message}", "JsonParseError", 400) { Exception = ex });
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
