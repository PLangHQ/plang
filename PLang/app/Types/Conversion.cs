using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using app.Data;

namespace app.Types;

/// <summary>
/// Conversion partial of <see cref="@this"/> — absorbs the former <c>Utils.TypeConverter</c>.
/// Public methods (ConvertTo, Populate, TryConvertTo) stay <c>public static</c>; pure-logic
/// helpers stay <c>private static</c> (Rule C exception class for stateless behaviour).
/// </summary>
public sealed partial class @this
{
    /// <summary>
    /// Local case-insensitive read options for the conversion path. Stage 27 dispersed
    /// the former <c>Utils.Json.CaseInsensitiveRead</c> static — http/code/Default holds
    /// its own copy too. Per-consumer ownership keeps Rule C closed without inventing a
    /// shared "Json" god-bag.
    ///
    /// Exposed via <see cref="CaseInsensitiveRead"/> (<c>internal</c>) so the test
    /// facade <c>App.Utils.Json.CaseInsensitiveRead</c> routes here instead of forking
    /// a fourth copy. Adding a converter here updates both this conversion path and the
    /// test surface in one place; <c>http/code/Default</c>'s separate copy stays
    /// independent (different consumer, different concern).
    /// </summary>
    internal static readonly JsonSerializerOptions _caseInsensitiveRead = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: true), new app.Data.EmptyStringToNullEnumConverterFactory(), new Channels.Serializers.TimeSpanIso8601() },
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    /// <summary>Internal accessor for the test facade — see <see cref="_caseInsensitiveRead"/>.</summary>
    internal static JsonSerializerOptions CaseInsensitiveRead => _caseInsensitiveRead;

    /// <summary>Attempts to convert a value to the specified type. Generic convenience overload.</summary>
    public static T? ConvertTo<T>(object? value) => (T?)ConvertTo(value, typeof(T));

    /// <summary>Attempts to convert a value to the specified type. Returns null on failure — use TryConvertTo for error details.</summary>
    public static object? ConvertTo(object? value, System.Type targetType)
    {
        var (result, _) = TryConvertTo(value, targetType);
        return result;
    }

    /// <summary>
    /// Populates an object's public writable properties from a dictionary.
    /// Keys are matched case-insensitively to property names. Values are converted via ConvertTo.
    /// </summary>
    public static void Populate(object target, IDictionary<string, object?> values)
    {
        foreach (var kvp in values)
        {
            var prop = target.GetType().GetProperty(kvp.Key,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop?.CanWrite != true) continue;
            var converted = ConvertTo(kvp.Value, prop.PropertyType);
            if (converted != null) prop.SetValue(target, converted);
        }
    }

    /// <summary>
    /// Attempts to convert a value to the specified type.
    /// Returns the converted value and null error on success,
    /// or null value and an Error describing what went wrong.
    /// </summary>
    public static (object? Value, Errors.Error? Error) TryConvertTo(object? value, System.Type targetType,
        Actor.Context.@this? context = null)
    {
        if (value == null)
            return (targetType.IsValueType ? System.Activator.CreateInstance(targetType) : null, null);

        var sourceType = value.GetType();
        if (targetType.IsAssignableFrom(sourceType))
            return (value, null);

        // Data.@this is the universal value wrapper — any value can become Data
        if (targetType == typeof(Data.@this) && value is not Data.@this)
            return (new Data.@this("", value), null);

        // Handle nullable target types
        var underlying = System.Nullable.GetUnderlyingType(targetType);
        if (underlying != null)
            return TryConvertTo(value, underlying, context);

        // String → JsonNode: use ToJson() extension with fix-and-retry
        if (targetType == typeof(JsonNode) && value is string jsonNodeStr)
        {
            var (node, jsonError) = jsonNodeStr.ToJson();
            if (jsonError is Errors.Error err) return (null, err);
            return (node, null);
        }

        // String → complex type: try JSON deserialization before list handling
        // (e.g., file.read of .pr returns JSON string → Goal)
        if (value is string jsonStr && !targetType.IsPrimitive && targetType != typeof(string))
        {
            try
            {
                var jsonResult = JsonSerializer.Deserialize(jsonStr, targetType, _caseInsensitiveRead);
                if (jsonResult != null) return (jsonResult, null);
            }
            catch (System.Exception ex) when (ex is JsonException || ex is NotSupportedException || ex is ArgumentException)
            {
                // If target is a single object but JSON is an array, try deserializing as List<T> and take first
                if (jsonStr.TrimStart().StartsWith('['))
                {
                    try
                    {
                        var listType = typeof(List<>).MakeGenericType(targetType);
                        var listResult = JsonSerializer.Deserialize(jsonStr, listType, _caseInsensitiveRead)
                            as System.Collections.IList;
                        if (listResult != null && listResult.Count > 0)
                            return (listResult[0], null);
                    }
                    catch (System.Exception inner) when (inner is JsonException || inner is NotSupportedException || inner is ArgumentException) { }
                }
            }
        }

        // List-like target: List<T> or types inheriting List<T>
        var listElementType = GetListElementType(targetType);
        if (listElementType != null)
        {
            // JsonElement-array source — enumerate elements and convert each. Without
            // this, a JSON-roundtripped List (Variables.Set's deep-clone path) would
            // be treated as a single value and only the array's "wrapper" would land
            // in a single-element list.
            if (value is JsonElement jeArr
                && jeArr.ValueKind == JsonValueKind.Array)
            {
                var targetList = (System.Collections.IList)System.Activator.CreateInstance(targetType)!;
                foreach (var elem in jeArr.EnumerateArray())
                {
                    var (convertedElem, _) = TryConvertTo(elem, listElementType, context);
                    if (convertedElem != null) targetList.Add(convertedElem);
                }
                return (targetList, null);
            }

            // JsonArray source — parallel to the JsonElement-array case above. JsonArray
            // implements IList<JsonNode?> but NOT the non-generic IList, so it skips the
            // generic-list arm below. Iterate its JsonNode items directly.
            if (value is JsonArray jArr)
            {
                var targetList = (System.Collections.IList)System.Activator.CreateInstance(targetType)!;
                foreach (var elem in jArr)
                {
                    var (convertedElem, _) = TryConvertTo(elem, listElementType, context);
                    if (convertedElem != null) targetList.Add(convertedElem);
                }
                return (targetList, null);
            }

            if (value is System.Collections.IList sourceList)
            {
                var targetList = (System.Collections.IList)System.Activator.CreateInstance(targetType)!;
                var errors = new List<Errors.Error>();
                for (int i = 0; i < sourceList.Count; i++)
                {
                    var (convertedItem, itemError) = TryConvertTo(sourceList[i], listElementType, context);
                    if (itemError != null)
                    {
                        itemError = new Errors.Error(
                            $"[{i}]: {itemError.Message}", "ElementConversionFailed", 400)
                            { FixSuggestion = itemError.FixSuggestion };
                        errors.Add(itemError);
                        continue;
                    }
                    if (convertedItem != null)
                        targetList.Add(convertedItem);
                }
                if (errors.Count > 0)
                {
                    var error = new Errors.Error(
                        $"Failed converting {errors.Count}/{sourceList.Count} elements from {sourceType.Name} to {targetType.Name}",
                        "ListConversionFailed", 400)
                        { FixSuggestion = $"Element type: {listElementType.Name}" };
                    foreach (var e in errors) error.ErrorChain.Add(e);
                    return (targetList.Count > 0 ? targetList : null, error);
                }
                return (targetList, null);
            }

            if (listElementType.IsAssignableFrom(sourceType))
            {
                var list = (System.Collections.IList)System.Activator.CreateInstance(targetType)!;
                list.Add(value);
                return (list, null);
            }
            var (converted, convError) = TryConvertTo(value, listElementType, context);
            if (converted != null && listElementType.IsAssignableFrom(converted.GetType()))
            {
                var list = (System.Collections.IList)System.Activator.CreateInstance(targetType)!;
                list.Add(converted);
                return (list, null);
            }
            if (convError != null)
                return (null, convError);
        }

        // Types with a constructor that accepts a single string (may have optional params).
        if (value is string ctorStr)
        {
            var ctor = targetType.GetConstructors()
                .FirstOrDefault(c =>
                {
                    var ps = c.GetParameters();
                    return ps.Length >= 1
                        && ps[0].ParameterType == typeof(string)
                        && ps.Skip(1).All(p => p.IsOptional);
                });
            if (ctor != null)
            {
                try
                {
                    var ps = ctor.GetParameters();
                    var args = new object?[ps.Length];
                    args[0] = ctorStr;
                    for (int ci = 1; ci < ps.Length; ci++)
                    {
                        if (context != null && ps[ci].ParameterType == typeof(Actor.Context.@this))
                            args[ci] = context;
                        else
                            args[ci] = ps[ci].DefaultValue;
                    }
                    return (ctor.Invoke(args), null);
                }
                catch (System.Exception ex) when (ex is not (System.NullReferenceException or System.OutOfMemoryException or System.StackOverflowException))
                {
                    return (null, new Errors.Error(ex.InnerException?.Message ?? ex.Message, "ConstructorFailed", 400));
                }
            }
        }

        // TimeSpan: parse ISO 8601 (e.g. "PT30S", "PT1H30M") via XmlConvert,
        // fall back to .NET TimeSpan.Parse (e.g. "00:30:00"). Same wire shape
        // the TimeSpanIso8601 uses for JSON.
        if (targetType == typeof(TimeSpan) && value is string tsStr)
        {
            try { return (System.Xml.XmlConvert.ToTimeSpan(tsStr), null); }
            catch (FormatException)
            {
                if (TimeSpan.TryParse(tsStr, System.Globalization.CultureInfo.InvariantCulture, out var ts))
                    return (ts, null);
                return (null, new Errors.Error(
                    $"Cannot parse '{tsStr}' as TimeSpan — expected ISO 8601 (e.g. PT30S) or .NET format (e.g. 00:00:30).",
                    "TimeSpanParseFailed", 400));
            }
        }

        // Enum types
        if (targetType.IsEnum)
        {
            if (value is string s)
            {
                if (System.Enum.TryParse(targetType, s, ignoreCase: true, out var parsed))
                    return (parsed, null);
                return (null, new Errors.Error(
                    $"Cannot parse '{s}' as {targetType.Name}",
                    "EnumParseFailed", 400)
                    { FixSuggestion = $"Valid values: {string.Join(", ", System.Enum.GetNames(targetType))}" });
            }
            if (value.GetType().IsEnum)
                return (value, null);
            try { return (System.Enum.ToObject(targetType, value), null); }
            catch (System.ArgumentException) { return (null, new Errors.Error(
                $"Cannot convert {sourceType.Name} to enum {targetType.Name}",
                "EnumConversionFailed", 400)); }
        }

        // GoalCall: convert from string, JsonElement, or Dictionary (UnwrapJsonElement output)
        if (targetType == typeof(app.Goals.Goal.GoalCall))
        {
            if (value is string goalName)
            {
                if (context?.App.Types.IsClrTypeName(goalName) ?? false)
                    return (null, new Errors.Error(
                        $"GoalCall.Name was set to a CLR type name '{goalName}' from a string source.",
                        "ClrTypeNameInGoalSlot", 500)
                        { FixSuggestion = "Build pipeline leaked a typed object's ToString() into a goal-name slot." });
                return (new app.Goals.Goal.GoalCall { Name = goalName }, null);
            }
            if (value is JsonElement je)
            {
                try
                {
                    return (JsonSerializer.Deserialize<app.Goals.Goal.GoalCall>(
                        je.GetRawText(),
                        _caseInsensitiveRead), null);
                }
                catch (System.Exception ex) when (ex is not (System.NullReferenceException or System.OutOfMemoryException or System.StackOverflowException))
                {
                    return (null, new Errors.Error(
                        $"Failed to deserialize GoalCall from JSON: {ex.Message}",
                        "GoalCallDeserializationFailed", 400));
                }
            }
            if (value is IDictionary<string, object?> dict)
            {
                var name = dict.TryGetValue("name", out var n) ? n?.ToString() ?? "" : "";
                if (context?.App.Types.IsClrTypeName(name) ?? false)
                    return (null, new Errors.Error(
                        $"GoalCall.Name was set to a CLR type name '{name}'.",
                        "ClrTypeNameInGoalSlot", 500)
                        { FixSuggestion = "Build pipeline leaked a typed object's ToString() into a goal-name slot " +
                            "(likely a Fluid template rendering an object via ToString() instead of navigating to .Name)." });
                var prPath = dict.TryGetValue("prPath", out var pr) ? pr?.ToString() : null;
                List<Data.@this>? parameters = null;
                if (dict.TryGetValue("parameters", out var p) && p is IList<object?> pList)
                {
                    parameters = pList
                        .OfType<IDictionary<string, object?>>()
                        .Select(d => new Data.@this(
                            d.TryGetValue("name", out var dn) ? dn?.ToString() ?? "" : "",
                            d.TryGetValue("value", out var dv) ? dv : null))
                        .ToList();
                }
                return (new app.Goals.Goal.GoalCall { Name = name, PrPath = prPath, Parameters = parameters }, null);
            }
        }

        // Primitives via Convert.ChangeType. InvariantCulture so JSON-shaped
        // numbers ("3.14", "1000") parse identically regardless of the user's locale.
        if (IsPrimitive(targetType))
        {
            try
            {
                return (System.Convert.ChangeType(value, targetType, System.Globalization.CultureInfo.InvariantCulture), null);
            }
            catch (System.Exception ex) when (ex is not (System.NullReferenceException or System.OutOfMemoryException or System.StackOverflowException))
            {
                return (null, new Errors.Error(
                    $"Cannot convert '{value}' ({sourceType.Name}) to {targetType.Name}: {ex.Message}",
                    "PrimitiveConversionFailed", 400));
            }
        }

        // Complex types: dict/JsonElement/JsonNode/list → serialize to JSON → deserialize to target type.
        if (value is IDictionary<string, object?> or JsonElement or JsonNode or System.Collections.IList)
        {
            string json = "";
            try
            {
                json = JsonSerializer.Serialize(value);
                var result = JsonSerializer.Deserialize(json, targetType, _caseInsensitiveRead);
                return (result, null);
            }
            catch (System.Exception ex) when (ex is not (System.NullReferenceException or System.OutOfMemoryException or System.StackOverflowException))
            {
                string jsonPreview;
                var posMatch = System.Text.RegularExpressions.Regex.Match(ex.Message, @"BytePositionInLine: (\d+)");
                if (posMatch.Success && int.TryParse(posMatch.Groups[1].Value, out var bytePos) && bytePos < json.Length)
                {
                    var start = System.Math.Max(0, bytePos - 100);
                    var end = System.Math.Min(json.Length, bytePos + 100);
                    jsonPreview = $"...{json[start..end]}...";
                }
                else
                {
                    var maxLen = context?.App?.Debug?.MaxLength ?? 500;
                    jsonPreview = json.Length > maxLen ? json[..maxLen] + $"... ({json.Length} chars)" : json;
                }
                return (null, new Errors.Error(
                    $"Failed to deserialize {sourceType.Name} to {targetType.Name}: {ex.Message}",
                    "DeserializationFailed", 400)
                    { FixSuggestion = $"JSON around error: {jsonPreview}" });
            }
        }

        // Last resort: type mismatch
        if (!targetType.IsAssignableFrom(sourceType))
        {
            return (null, new Errors.Error(
                FormatTypeMismatch(value, sourceType, targetType),
                "TypeMismatch", 400)
                { FixSuggestion = TypeMismatchHint(value, sourceType, targetType) });
        }

        return (value, null);
    }

    private static string FormatTypeMismatch(object? value, System.Type sourceType, System.Type targetType)
    {
        return $"Cannot convert {sourceType.FullName} to {targetType.FullName}. Source value: {FormatValuePreview(value)}";
    }

    private static string TypeMismatchHint(object? value, System.Type sourceType, System.Type targetType)
    {
        if (value is string s && s.Contains('%'))
            return $"Source value contains '%' — likely an unresolved %var% reference. Check that the variable is set and reachable in the current context, or that the dot-path navigation matches the value's actual shape.";
        return $"Source: {sourceType.FullName}, Target: {targetType.FullName}";
    }

    private static string FormatValuePreview(object? value)
    {
        if (value == null) return "(null)";
        if (value is string s)
        {
            var len = s.Length;
            if (len <= 100) return $"\"{s}\" (string, {len} chars)";
            return $"\"{s[..100]}…\" (string, {len} chars)";
        }
        if (value is System.Collections.ICollection col)
            return $"<{value.GetType().Name} @ {col.Count} items>";
        var str = value.ToString() ?? "?";
        return str.Length <= 100 ? $"{str} ({value.GetType().Name})" : $"{str[..100]}… ({value.GetType().Name})";
    }

    private static System.Type? GetListElementType(System.Type targetType)
    {
        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
            return targetType.GetGenericArguments()[0];

        var baseType = targetType.BaseType;
        while (baseType != null)
        {
            if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(List<>))
                return baseType.GetGenericArguments()[0];
            baseType = baseType.BaseType;
        }

        return null;
    }
}
