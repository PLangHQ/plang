using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using App;

namespace App.Utils;

/// <summary>
/// Converts values between CLR types. Extracted from TypeMapping so that class
/// can stay focused on name/type identity — this class owns the conversion rules
/// (primitive widening, JSON de/serialization, list wrapping, enum parsing,
/// IObject ctor, GoalCall reconstitution, …).
/// </summary>
public static class TypeConverter
{
    /// <summary>
    /// Attempts to convert a value to the specified type. Generic convenience overload.
    /// </summary>
    public static T? ConvertTo<T>(object? value) => (T?)ConvertTo(value, typeof(T));

    /// <summary>
    /// Attempts to convert a value to the specified type.
    /// Returns null on failure — use TryConvertTo for error details.
    /// </summary>
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
                var jsonResult = System.Text.Json.JsonSerializer.Deserialize(jsonStr, targetType, Json.CaseInsensitiveRead);
                if (jsonResult != null) return (jsonResult, null);
            }
            catch (System.Exception ex) when (ex is System.Text.Json.JsonException || ex is NotSupportedException || ex is ArgumentException)
            {
                // If target is a single object but JSON is an array, try deserializing as List<T> and take first
                if (jsonStr.TrimStart().StartsWith('['))
                {
                    try
                    {
                        var listType = typeof(List<>).MakeGenericType(targetType);
                        var listResult = System.Text.Json.JsonSerializer.Deserialize(jsonStr, listType, Json.CaseInsensitiveRead)
                            as System.Collections.IList;
                        if (listResult != null && listResult.Count > 0)
                            return (listResult[0], null);
                    }
                    catch (System.Exception inner) when (inner is System.Text.Json.JsonException || inner is NotSupportedException || inner is ArgumentException) { }
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
            if (value is System.Text.Json.JsonElement jeArr
                && jeArr.ValueKind == System.Text.Json.JsonValueKind.Array)
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

        // IObject types — validated value types with string constructors
        if (typeof(App.modules.IObject).IsAssignableFrom(targetType))
        {
            var strVal = value is string sv ? sv : value?.ToString();
            if (strVal == null)
                return (null, new Errors.Error(
                    $"Cannot convert null to {targetType.Name}",
                    "IObjectConversionFailed", 400));
            var ctor = targetType.GetConstructor([typeof(string)]);
            if (ctor == null)
                return (null, new Errors.Error(
                    $"{targetType.Name} has no string constructor",
                    "IObjectConversionFailed", 400));
            try
            {
                return (ctor.Invoke([strVal]), null);
            }
            catch (System.Exception ex) when (ex is not (System.NullReferenceException or System.OutOfMemoryException or System.StackOverflowException))
            {
                var inner = ex.InnerException ?? ex;
                var validValues = TypeMapping.GetValidValues(targetType);
                return (null, new Errors.Error(
                    inner.Message,
                    "IObjectConversionFailed", 400)
                    { FixSuggestion = validValues != null
                        ? $"Valid values: {string.Join(", ", validValues)}"
                        : null });
            }
        }

        // Types with a constructor that accepts a single string (may have optional params)
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
                    // Pass context to any optional Actor.Context.@this parameter so
                    // runtime-aware types (Path, etc.) keep their Context wired
                    // when reconstituted from strings during build/enrich.
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
        if (targetType == typeof(App.Goals.Goal.GoalCall))
        {
            if (value is string goalName)
            {
                if (PlangTypeIndex.IsClrTypeName(goalName))
                    return (null, new Errors.Error(
                        $"GoalCall.Name was set to a CLR type name '{goalName}' from a string source.",
                        "ClrTypeNameInGoalSlot", 500)
                        { FixSuggestion = "Build pipeline leaked a typed object's ToString() into a goal-name slot." });
                return (new App.Goals.Goal.GoalCall { Name = goalName }, null);
            }
            if (value is System.Text.Json.JsonElement je)
            {
                try
                {
                    return (System.Text.Json.JsonSerializer.Deserialize<App.Goals.Goal.GoalCall>(
                        je.GetRawText(),
                        Json.CaseInsensitiveRead), null);
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
                // Runtime guard: a CLR type FullName has no business being a goal name.
                // Tripwire for a known leak vector (Fluid template rendering an object as
                // ToString()) — left here defensively even though the rebuild that fixed
                // buildstep.pr did not retrigger it. Remove once we've gone several
                // bootstrap cycles without it firing and the original leak path is
                // identified or proven extinct.
                if (PlangTypeIndex.IsClrTypeName(name))
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
                return (new App.Goals.Goal.GoalCall { Name = name, PrPath = prPath, Parameters = parameters }, null);
            }
        }

        // Primitives via Convert.ChangeType. InvariantCulture so JSON-shaped
        // numbers ("3.14", "1000") parse identically regardless of the user's
        // locale — without this, "3.14" → double FormatExceptions on it-IT,
        // de-DE, etc. that expect "3,14".
        if (TypeMapping.IsPrimitive(targetType))
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
        // JsonNode covers JsonObject/JsonArray/JsonValue (the System.Text.Json mutable view) — without
        // it, a value stored via `set ... type=json` (which mints Data<JsonNode>) cannot reach a
        // strongly-typed handler property: JsonObject implements IDictionary<string, JsonNode?>, NOT
        // IDictionary<string, object?>, so it slips past the first dispatch arm.
        if (value is IDictionary<string, object?> or System.Text.Json.JsonElement or JsonNode or System.Collections.IList)
        {
            string json = "";
            try
            {
                json = System.Text.Json.JsonSerializer.Serialize(value);
                var result = System.Text.Json.JsonSerializer.Deserialize(json, targetType, Json.CaseInsensitiveRead);
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
                $"Cannot convert {sourceType.Name} to {targetType.Name}",
                "TypeMismatch", 400)
                { FixSuggestion = $"Source: {sourceType.FullName}, Target: {targetType.FullName}" });
        }

        return (value, null);
    }

    /// <summary>
    /// Finds the element type if targetType is List&lt;T&gt; or inherits from it.
    /// Returns null if not a list type.
    /// </summary>
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
