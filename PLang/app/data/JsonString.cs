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
    /// Parses the string as JSON. Layered fallbacks — each tries the previous fix
    /// PLUS one more transform; returns the first that parses cleanly.
    ///   1. strict JSON
    ///   2. unescaped control characters in string values fixed
    ///   3. bareword object keys quoted (PLang brace-literal shorthand
    ///      <c>{a:{b:1}}</c> → <c>{"a":{"b":1}}</c>)
    ///   4. trailing prose after JSON stripped (find balanced-brace prefix; useful
    ///      when an LLM wraps its JSON in "Here's the result: {...} hope this helps")
    ///   5. truncation repair — unmatched openers get matching closes appended
    ///      (catches the "model dropped the last close at depth N" case)
    ///   6. trailing commas before <c>}</c>/<c>]</c> stripped
    /// Returns error only when all stages fail. The error message includes the
    /// JSON exception detail AND a preview of the content; full content lives in
    /// <c>Details["Content"]</c>.
    /// </summary>
    public static (JsonNode? result, error.IError? error) ToJson(this string str)
    {
        try { return (JsonNode.Parse(str), null); }
        catch (JsonException) { }

        try
        {
            var fixedJson = FixJsonStringValues(str);
            return (JsonNode.Parse(fixedJson), null);
        }
        catch (JsonException) { }

        try
        {
            var fixedJson = QuoteBarewordKeys(FixJsonStringValues(str));
            return (JsonNode.Parse(fixedJson), null);
        }
        catch (JsonException) { }

        // Stage 4: trailing prose strip.
        var sliced = SliceBalancedPrefix(str);
        if (sliced != null)
        {
            try { return (JsonNode.Parse(sliced), null); }
            catch (JsonException) { }
            try { return (JsonNode.Parse(StripTrailingCommas(sliced)), null); }
            catch (JsonException) { }
        }

        // Stage 5: truncation repair.
        var closed = AppendUnmatchedCloses(str);
        if (closed != null)
        {
            try { return (JsonNode.Parse(closed), null); }
            catch (JsonException) { }
            try { return (JsonNode.Parse(StripTrailingCommas(closed)), null); }
            catch (JsonException) { }
        }

        // Stage 6: just the trailing-comma strip on the original.
        try { return (JsonNode.Parse(StripTrailingCommas(str)), null); }
        catch (JsonException ex)
        {
            const int MaxInMessage = 500;
            var preview = str.Length > MaxInMessage
                ? str[..MaxInMessage] + $"... ({str.Length} chars total)"
                : str;
            return (null, new error.ActionError(
                $"Invalid JSON: {ex.Message}\n\nContent that failed to parse:\n{preview}",
                "JsonParseError", 400)
            {
                Exception = ex,
                Details = new Dictionary<string, object?>
                {
                    ["Content"] = str,
                    ["JsonException"] = ex.Message,
                    ["LineNumber"] = ex.LineNumber,
                    ["BytePositionInLine"] = ex.BytePositionInLine
                }
            });
        }
    }

    /// <summary>
    /// Wraps unquoted object keys in double quotes. PLang surface accepts brace-literal
    /// shorthand <c>{a:{b:1}}</c>; the wire format is strict JSON.
    /// </summary>
    internal static string QuoteBarewordKeys(string json)
    {
        var sb = new StringBuilder(json.Length + 16);
        bool inString = false;
        int i = 0;
        while (i < json.Length)
        {
            char c = json[i];
            if (c == '"' && (i == 0 || json[i - 1] != '\\'))
            {
                inString = !inString;
                sb.Append(c);
                i++;
                continue;
            }
            if (inString) { sb.Append(c); i++; continue; }
            if (c == '{' || c == ',')
            {
                sb.Append(c);
                i++;
                while (i < json.Length && char.IsWhiteSpace(json[i])) { sb.Append(json[i]); i++; }
                if (i < json.Length && (char.IsLetter(json[i]) || json[i] == '_' || json[i] == '$'))
                {
                    int identStart = i;
                    while (i < json.Length && (char.IsLetterOrDigit(json[i]) || json[i] == '_' || json[i] == '$')) i++;
                    int identEnd = i;
                    int afterWs = i;
                    while (afterWs < json.Length && char.IsWhiteSpace(json[afterWs])) afterWs++;
                    if (afterWs < json.Length && json[afterWs] == ':')
                    {
                        sb.Append('"').Append(json, identStart, identEnd - identStart).Append('"');
                        sb.Append(json, identEnd, afterWs - identEnd);
                        i = afterWs;
                    }
                    else
                    {
                        sb.Append(json, identStart, identEnd - identStart);
                    }
                }
                continue;
            }
            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }

    /// <summary>
    /// Walks str with a string-aware scanner. If braces/brackets balance to zero
    /// BEFORE end-of-input, returns the prefix up through that point (strips trailing
    /// prose). Returns null when input doesn't start with <c>{</c>/<c>[</c> or never
    /// balances early.
    /// </summary>
    internal static string? SliceBalancedPrefix(string str)
    {
        var trimmed = str.AsSpan().TrimStart();
        if (trimmed.Length == 0) return null;
        var opening = trimmed[0];
        if (opening != '{' && opening != '[') return null;
        int leading = str.Length - trimmed.Length;

        int depth = 0;
        bool inString = false, escape = false;
        for (int i = 0; i < trimmed.Length; i++)
        {
            var c = trimmed[i];
            if (escape) { escape = false; continue; }
            if (c == '\\' && inString) { escape = true; continue; }
            if (c == '"') { inString = !inString; continue; }
            if (inString) continue;
            if (c == '{' || c == '[') depth++;
            else if (c == '}' || c == ']')
            {
                depth--;
                if (depth == 0 && i < trimmed.Length - 1)
                    return str[..(leading + i + 1)];
                if (depth == 0) return null;
            }
        }
        return null;
    }

    /// <summary>
    /// Walks str tracking unmatched openers on a stack. Appends matching closes if
    /// any remain unclosed at end-of-input. Returns null when no repair is needed.
    /// </summary>
    internal static string? AppendUnmatchedCloses(string str)
    {
        var trimmed = str.AsSpan().TrimStart();
        if (trimmed.Length == 0) return null;
        var opening = trimmed[0];
        if (opening != '{' && opening != '[') return null;

        var stack = new Stack<char>();
        bool inString = false, escape = false;
        foreach (var c in trimmed)
        {
            if (escape) { escape = false; continue; }
            if (c == '\\' && inString) { escape = true; continue; }
            if (c == '"') { inString = !inString; continue; }
            if (inString) continue;
            if (c == '{') stack.Push('}');
            else if (c == '[') stack.Push(']');
            else if (c == '}' || c == ']') { if (stack.Count > 0) stack.Pop(); }
        }
        if (stack.Count == 0) return null;

        var sb = new StringBuilder(str);
        while (stack.Count > 0) sb.Append(stack.Pop());
        return sb.ToString();
    }

    /// <summary>Strips trailing commas before <c>}</c> or <c>]</c>.</summary>
    internal static string StripTrailingCommas(string s)
        => System.Text.RegularExpressions.Regex.Replace(s, @",(\s*[}\]])", "$1");

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
/// Used by <see cref="app.types.@this"/>'s case-insensitive read options and by HTTP transport.
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
