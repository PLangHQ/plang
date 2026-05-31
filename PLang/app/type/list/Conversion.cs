using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using app.data;

namespace app.type.list;

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
        Converters = {
            new JsonStringEnumConverter(allowIntegerValues: true),
            new app.data.EmptyStringToNullEnumConverterFactory(),
            new global::app.channel.serializer.TimeSpanIso8601(),
            // Context-less PathJsonConverter — produces stub Paths. Callers
            // with a Context in scope use ContextualReadOptions instead so
            // deserialized Paths are wired immediately.
            new global::app.type.path.JsonConverter(),
        },
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    /// <summary>
    /// Builds a one-shot JsonSerializerOptions equivalent to
    /// <see cref="_caseInsensitiveRead"/> but with a Context-bound
    /// <see cref="app.type.path.JsonConverter"/> in place of the stub one.
    /// Used when <see cref="TryConvertTo"/> receives a non-null context so
    /// every <see cref="app.type.path.@this"/> field in the deserialized
    /// graph lands fully Context-wired.
    /// </summary>
    private static JsonSerializerOptions ContextualReadOptions(actor.context.@this context)
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = {
                new JsonStringEnumConverter(allowIntegerValues: true),
                new app.data.EmptyStringToNullEnumConverterFactory(),
                new global::app.channel.serializer.TimeSpanIso8601(),
                new global::app.type.path.JsonConverter(context),
            },
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };
    }

    /// <summary>Internal accessor for the test facade — see <see cref="_caseInsensitiveRead"/>.</summary>
    internal static JsonSerializerOptions CaseInsensitiveRead => _caseInsensitiveRead;

    /// <summary>Attempts to convert a value to the specified type. Generic convenience overload.</summary>
    public static T? ConvertTo<T>(object? value, actor.context.@this? context = null)
        => (T?)ConvertTo(value, typeof(T), context);

    /// <summary>
    /// Attempts to convert a value to the specified type. Returns null on failure — use TryConvertTo for error details.
    /// A <paramref name="context"/> is required to convert a string into a <see cref="path.@this"/> (the per-App
    /// scheme registry needs it); without one, string→path conversions yield null.
    /// </summary>
    public static object? ConvertTo(object? value, System.Type targetType, actor.context.@this? context = null)
    {
        var (result, _) = TryConvertTo(value, targetType, context);
        return result;
    }

    /// <summary>
    /// Populates an object's public writable properties from a dictionary.
    /// Keys are matched case-insensitively to property names. Values are converted via ConvertTo.
    /// Pass <paramref name="context"/> when any target property is <see cref="path.@this"/>-typed
    /// (or a list of them) — without it those properties stay unset.
    /// </summary>
    public static void Populate(object target, IDictionary<string, object?> values,
        actor.context.@this? context = null)
    {
        foreach (var kvp in values)
        {
            var prop = target.GetType().GetProperty(kvp.Key,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop?.CanWrite != true) continue;
            var converted = ConvertTo(kvp.Value, prop.PropertyType, context);
            if (converted != null) prop.SetValue(target, converted);
        }
    }

    /// <summary>
    /// Attempts to convert a value to the specified type.
    /// Returns the converted value and null error on success,
    /// or null value and an Error describing what went wrong.
    /// </summary>
    public static (object? Value, error.Error? Error) TryConvertTo(object? value, System.Type targetType,
        actor.context.@this? context = null, string? targetName = null)
    {
        if (value == null)
            return (targetType.IsValueType ? System.Activator.CreateInstance(targetType) : null, null);

        var sourceType = value.GetType();
        if (targetType.IsAssignableFrom(sourceType))
            return (value, null);

        // data.@this is the universal value wrapper — any value can become Data
        if (targetType == typeof(data.@this) && value is not data.@this)
            return (new data.@this("", value), null);

        // Handle nullable target types
        var underlying = System.Nullable.GetUnderlyingType(targetType);
        if (underlying != null)
            return TryConvertTo(value, underlying, context, targetName);

        // String → JsonNode: use ToJson() extension with fix-and-retry
        if (targetType == typeof(JsonNode) && value is string jsonNodeStr)
        {
            var (node, jsonError) = jsonNodeStr.ToJson();
            if (jsonError is error.Error err) return (null, err);
            return (node, null);
        }

        // String → complex type: try JSON deserialization before list handling
        // (e.g., file.read of .pr returns JSON string → Goal)
        if (value is string jsonStr && !targetType.IsPrimitive && targetType != typeof(string))
        {
            // Context-bound options when the caller passed one — deserialised
            // Paths get path.Resolve(raw, context) treatment so they land Context-
            // wired. Falls back to the static stub-Path options otherwise.
            var readOpts = context != null ? ContextualReadOptions(context) : _caseInsensitiveRead;
            try
            {
                var jsonResult = JsonSerializer.Deserialize(jsonStr, targetType, readOpts);
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
                        var listResult = JsonSerializer.Deserialize(jsonStr, listType, readOpts)
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
                var errors = new List<error.Error>();
                for (int i = 0; i < sourceList.Count; i++)
                {
                    var (convertedItem, itemError) = TryConvertTo(sourceList[i], listElementType, context);
                    if (itemError != null)
                    {
                        itemError = new error.Error(
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
                    var error = new error.Error(
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

        // Path: route through the per-App scheme registry. The abstract base
        // can't be constructed directly; the registry dispatches to the right
        // subclass (file → FilePath, http/https → HttpPath, …) based on the
        // raw string's scheme prefix.
        if (value is string rawPath
            && context != null
            && typeof(global::app.type.path.@this).IsAssignableFrom(targetType))
        {
            try
            {
                return (context.App.Type.Scheme.From(rawPath, context), null);
            }
            catch (global::app.type.path.scheme.SchemeNotRegistered snr)
            {
                return (null, new error.Error(snr.Message, "SchemeNotRegistered", 400)
                    { FixSuggestion = $"Register a factory for scheme '{snr.Scheme}' via app.Type.Scheme.Register, or use a bare/file:// path." });
            }
            catch (System.Exception ex) when (ex is not (System.NullReferenceException or System.OutOfMemoryException or System.StackOverflowException))
            {
                return (null, new error.Error(ex.Message, "PathConstructionFailed", 400));
            }
        }

        // Reference fundamental backed by a path (image; audio/video follow the
        // same shape): a path-string mints a LAZY handle with .Path set — no
        // content read. The type exposes a single-arg constructor taking a
        // path.@this; resolve the string through the scheme registry (no I/O —
        // just constructs the path), then build the handle. Content materializes
        // later, on first await of the handle's async accessor.
        if (value is string refPathRaw && context != null
            && !typeof(global::app.type.path.@this).IsAssignableFrom(targetType))
        {
            var pathCtor = targetType.GetConstructors().FirstOrDefault(c =>
            {
                var ps = c.GetParameters();
                return ps.Length >= 1
                    && typeof(global::app.type.path.@this).IsAssignableFrom(ps[0].ParameterType)
                    && ps.Skip(1).All(p => p.IsOptional);
            });
            if (pathCtor != null)
            {
                try
                {
                    var handlePath = context.App.Type.Scheme.From(refPathRaw, context);
                    var ps = pathCtor.GetParameters();
                    var args = new object?[ps.Length];
                    args[0] = handlePath;
                    for (int i = 1; i < ps.Length; i++) args[i] = ps[i].DefaultValue;
                    return (pathCtor.Invoke(args), null);
                }
                catch (global::app.type.path.scheme.SchemeNotRegistered snr)
                {
                    return (null, new error.Error(snr.Message, "SchemeNotRegistered", 400)
                        { FixSuggestion = $"Register a factory for scheme '{snr.Scheme}', or use a bare/file:// path." });
                }
                catch (System.Exception ex) when (ex is not (System.NullReferenceException or System.OutOfMemoryException or System.StackOverflowException))
                {
                    return (null, new error.Error(ex.InnerException?.Message ?? ex.Message, "PathHandleConstructionFailed", 400));
                }
            }
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
                        if (context != null && ps[ci].ParameterType == typeof(actor.context.@this))
                            args[ci] = context;
                        else
                            args[ci] = ps[ci].DefaultValue;
                    }
                    return (ctor.Invoke(args), null);
                }
                catch (System.Exception ex) when (ex is not (System.NullReferenceException or System.OutOfMemoryException or System.StackOverflowException))
                {
                    return (null, new error.Error(ex.InnerException?.Message ?? ex.Message, "ConstructorFailed", 400));
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
                return (null, new error.Error(
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
                return (null, new error.Error(
                    $"Cannot parse '{s}' as {targetType.Name}",
                    "EnumParseFailed", 400)
                    { FixSuggestion = $"Valid values: {string.Join(", ", System.Enum.GetNames(targetType))}" });
            }
            if (value.GetType().IsEnum)
                return (value, null);
            try { return (System.Enum.ToObject(targetType, value), null); }
            catch (System.ArgumentException) { return (null, new error.Error(
                $"Cannot convert {sourceType.Name} to enum {targetType.Name}",
                "EnumConversionFailed", 400)); }
        }

        // GoalCall: convert from string, JsonElement, or Dictionary (UnwrapJsonElement output)
        if (targetType == typeof(app.goal.GoalCall))
        {
            if (value is string goalName)
            {
                if (context?.App.Type.IsClrTypeName(goalName) ?? false)
                    return (null, new error.Error(
                        $"GoalCall.Name was set to a CLR type name '{goalName}' from a string source.",
                        "ClrTypeNameInGoalSlot", 500)
                        { FixSuggestion = "Build pipeline leaked a typed object's ToString() into a goal-name slot." });
                return (new app.goal.GoalCall { Name = goalName }, null);
            }
            if (value is JsonElement je)
            {
                try
                {
                    return (JsonSerializer.Deserialize<app.goal.GoalCall>(
                        je.GetRawText(),
                        _caseInsensitiveRead), null);
                }
                catch (System.Exception ex) when (ex is not (System.NullReferenceException or System.OutOfMemoryException or System.StackOverflowException))
                {
                    return (null, new error.Error(
                        $"Failed to deserialize GoalCall from JSON: {ex.Message}",
                        "GoalCallDeserializationFailed", 400));
                }
            }
            if (value is IDictionary<string, object?> dict)
            {
                var name = dict.TryGetValue("name", out var n) ? n?.ToString() ?? "" : "";
                if (context?.App.Type.IsClrTypeName(name) ?? false)
                    return (null, new error.Error(
                        $"GoalCall.Name was set to a CLR type name '{name}'.",
                        "ClrTypeNameInGoalSlot", 500)
                        { FixSuggestion = "Build pipeline leaked a typed object's ToString() into a goal-name slot " +
                            "(likely a Fluid template rendering an object via ToString() instead of navigating to .Name)." });
                var prPathStr = dict.TryGetValue("prPath", out var pr) ? pr?.ToString() : null;
                var prPath = (prPathStr != null && context != null)
                    ? global::app.type.path.@this.Resolve(prPathStr, context)
                    : null;
                List<data.@this>? parameters = null;
                if (dict.TryGetValue("parameters", out var p) && p is IList<object?> pList)
                {
                    parameters = pList
                        .OfType<IDictionary<string, object?>>()
                        .Select(d => new data.@this(
                            d.TryGetValue("name", out var dn) ? dn?.ToString() ?? "" : "",
                            d.TryGetValue("value", out var dv) ? dv : null))
                        .ToList();
                }
                return (new app.goal.GoalCall { Name = name, PrPath = prPath, Parameters = parameters }, null);
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
                // Lead with target type + parameter name + actual content; never
                // surface the raw C# exception text (e.g. "Object must implement
                // IConvertible") as the headline — it's meaningless to a PLang dev.
                var convErr = new error.Error(
                    BindFailureMessage(value, sourceType, targetType, targetName),
                    "PrimitiveConversionFailed", 400)
                { Exception = ex };
                if (value is error.Error sourceErr)
                {
                    sourceErr.ErrorChain.Add(convErr);
                    return (null, sourceErr);
                }
                return (null, convErr);
            }
        }

        // Complex types: dict/JsonElement/JsonNode/list → serialize to JSON → deserialize to target type.
        if (value is IDictionary<string, object?> or JsonElement or JsonNode or System.Collections.IList)
        {
            string json = "";
            try
            {
                // Serialize with the same converter set as the read side so a
                // path.@this nested in the dict goes through PathJsonConverter
                // (string form) instead of being reflected into a full object
                // graph that the read side then can't deserialize.
                var writeOpts = context != null ? ContextualReadOptions(context) : _caseInsensitiveRead;
                json = JsonSerializer.Serialize(value, writeOpts);
                var result = JsonSerializer.Deserialize(json, targetType, writeOpts);
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
                return (null, new error.Error(
                    $"Failed to deserialize {sourceType.Name} to {targetType.Name}: {ex.Message}",
                    "DeserializationFailed", 400)
                    { FixSuggestion = $"JSON around error: {jsonPreview}" });
            }
        }

        // Last resort: type mismatch
        if (!targetType.IsAssignableFrom(sourceType))
        {
            return (null, new error.Error(
                FormatTypeMismatch(value, sourceType, targetType, targetName),
                "TypeMismatch", 400)
                { FixSuggestion = TypeMismatchHint(value, sourceType, targetType) });
        }

        return (value, null);
    }

    /// <summary>
    /// A parameter-binding failure in plain language: what we tried to convert
    /// <em>to</em> (target PLang type), <em>where</em> (the parameter name, when
    /// the binding layer threaded it in), and <em>from</em> (the actual value's
    /// type + a content preview). Leads with these three facts and never the raw
    /// C# exception text.
    /// </summary>
    private static string BindFailureMessage(object? value, System.Type sourceType, System.Type targetType, string? targetName)
    {
        var expected = PlangTypeLabel(targetType);
        var slot = string.IsNullOrEmpty(targetName) ? "" : $" parameter '{targetName}'";
        var actual = value is error.Error err
            ? $"an Error object ({err.Key}: {Truncate(err.Message, 120)})"
            : $"{PlangTypeLabel(sourceType)} {FormatValuePreview(value)}";
        return $"Could not bind{slot}: expected {expected} but the value is {actual}.";
    }

    /// <summary>PLang type name + CLR type for a target/source, e.g. "text (string)".</summary>
    private static string PlangTypeLabel(System.Type type)
    {
        var u = System.Nullable.GetUnderlyingType(type) ?? type;
        return app.type.primitive.@this.Canonical.TryGetValue(u, out var plang)
            ? $"{plang} ({u.Name})"
            : u.Name;
    }

    private static string Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) ? "" : (s!.Length <= max ? s : s[..max] + "…");

    private static string FormatTypeMismatch(object? value, System.Type sourceType, System.Type targetType, string? targetName = null)
    {
        // FullName (not Name) so an OBP `@this` target disambiguates; value
        // preview surfaces an unresolved %var% in the headline. Lead with the
        // parameter name when the binding layer threaded it in.
        var slot = string.IsNullOrEmpty(targetName) ? "" : $"parameter '{targetName}': ";
        return $"{slot}Cannot convert {sourceType.FullName} to {targetType.FullName}. Source value: {FormatValuePreview(value)}";
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
