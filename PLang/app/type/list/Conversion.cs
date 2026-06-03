using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using app.data;

namespace app.type.list;

/// <summary>
/// Conversion partial of <see cref="@this"/>. The public surface is the infra dispatch
/// door <see cref="Convert(object?, System.Type, actor.context.@this?, string?)"/>; the
/// shared dispatcher (<c>TryConvert</c>) + its helpers stay <c>internal static</c> — the
/// type-agnostic plumbing + residual primitive leaf both doors share. Per-type construction
/// knowledge lives on the owning types (their <c>Convert</c> hooks), reached here via the
/// hook dispatch; this file holds no per-type arms. Pure-logic helpers stay
/// <c>private static</c> (Rule C stateless behaviour).
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
            // Context-less json Converter — produces stub Paths. Callers
            // with a Context in scope use ContextualReadOptions instead so
            // deserialized Paths are wired immediately.
            new global::app.channel.serializer.json.Converter(),
        },
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    /// <summary>
    /// Builds a one-shot JsonSerializerOptions equivalent to
    /// <see cref="_caseInsensitiveRead"/> but with a Context-bound
    /// <see cref="app.type.path.JsonConverter"/> in place of the stub one.
    /// Used when <see cref="TryConvert"/> receives a non-null context so
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
                new global::app.channel.serializer.json.Converter(context),
            },
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };
    }

    /// <summary>Internal accessor for the test facade — see <see cref="_caseInsensitiveRead"/>.</summary>
    internal static JsonSerializerOptions CaseInsensitiveRead => _caseInsensitiveRead;

    /// <summary>
    /// Infra dispatch door — used by callers that hold only a CLR target (Data.As&lt;T&gt;,
    /// wire reconstruct, the variable navigator, Sqlite, builder validators). Resolves the
    /// owning family from <paramref name="clrTarget"/>, asks its <c>Convert</c> hook, and
    /// falls through to the residual primitive leaf + plumbing in <see cref="TryConvert"/>.
    /// The primary door (<c>type.@this.Convert</c>) is for callers holding a PLang type entity.
    /// <paramref name="slot"/> names the binding target so an action-parameter bind failure
    /// reads naturally.
    /// </summary>
    public global::app.data.@this Convert(object? value, System.Type clrTarget,
        actor.context.@this? context = null, string? slot = null)
    {
        var (result, error) = TryConvert(value, clrTarget, context, slot);
        return error != null ? global::app.data.@this.FromError(error) : global::app.data.@this.Ok(result);
    }

    /// <summary>Attempts to convert a value to the specified type. Generic convenience overload.</summary>
    internal static T? ConvertTo<T>(object? value, actor.context.@this? context = null)
        => (T?)ConvertTo(value, typeof(T), context);

    /// <summary>
    /// Attempts to convert a value to the specified type. Returns null on failure — use TryConvert for error details.
    /// A <paramref name="context"/> is required to convert a string into a <see cref="path.@this"/> (the per-App
    /// scheme registry needs it); without one, string→path conversions yield null.
    /// </summary>
    internal static object? ConvertTo(object? value, System.Type targetType, actor.context.@this? context = null)
    {
        var (result, _) = TryConvert(value, targetType, context);
        return result;
    }

    /// <summary>
    /// Populates an object's public writable properties from a dictionary.
    /// Keys are matched case-insensitively to property names. Values are converted via ConvertTo.
    /// Pass <paramref name="context"/> when any target property is <see cref="path.@this"/>-typed
    /// (or a list of them) — without it those properties stay unset.
    /// </summary>
    internal static void Populate(object target, IDictionary<string, object?> values,
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
    internal static (object? Value, error.Error? Error) TryConvert(object? value, System.Type targetType,
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
            return TryConvert(value, underlying, context, targetName);

        // OBP: ask the owning type to build the value — the type knows its own
        // construction (number/text/datetime/duration/path/image/goal.call). When a
        // family owns the target it is authoritative: success returns the value, an
        // error surfaces (enriched with the slot name). A null result means the family
        // declined this value shape (e.g. image for raw bytes) — fall through to the
        // residual leaf + plumbing below.
        if (context != null)
        {
            var (family, kind) = global::app.type.convert.@this.OwnerOf(targetType);
            if (family != null)
            {
                var owned = context.App.Type.Conversions.Of(family, value, kind, context);
                if (owned != null)
                {
                    if (owned.Success) return (owned.Value, null);
                    var hookErr = owned.Error as error.Error
                        ?? new error.Error(owned.Error!.Message, "TypeConversionFailed", 400);
                    return (null, WithSlot(hookErr, targetName));
                }
            }
        }

        // String → JsonNode: use ToJson() extension with fix-and-retry
        if (targetType == typeof(JsonNode) && value is string jsonNodeStr)
        {
            var (node, jsonError) = jsonNodeStr.ToJson();
            if (jsonError is error.Error err) return (null, err);
            return (node, null);
        }

        // A target type that owns its wire reconstruction — a static
        // `object? FromWire(string, string?)` — rebuilds from the raw string
        // itself (the snapshot / crypto.hash seam). Checked before the generic
        // JSON deserialize below, which would produce a broken object for types
        // whose state isn't plain public properties (snapshot's private section
        // tree). Kind is null here — FromWire types needing a kind carry it on
        // the type entity, not on this conversion path.
        if (value is string wireStr)
        {
            var reader = global::app.type.@this.WireReader(targetType);
            if (reader != null)
            {
                var built = reader.Invoke(null, new object?[] { wireStr, null });
                if (built != null) return (built, null);
            }
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
                    var (convertedElem, _) = TryConvert(elem, listElementType, context);
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
                    var (convertedElem, _) = TryConvert(elem, listElementType, context);
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
                    var (convertedItem, itemError) = TryConvert(sourceList[i], listElementType, context);
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

            // The value IS a collection, but none of the arms above recognized it
            // (e.g. a generic-only IList<T>/IEnumerable<T> that isn't the non-generic
            // IList — a domain collection like step.actions.@this). Falling through to
            // the single-element wrap below would silently bury the whole collection in
            // one slot and hide the gap. Throw loudly so it surfaces and the list
            // converter grows a real arm for this shape instead of masking it.
            if (value is System.Collections.IEnumerable && value is not string)
                throw new System.InvalidOperationException(
                    $"List-conversion gap: cannot convert {sourceType.FullName} into {targetType.FullName}. " +
                    "It is a collection the list converter doesn't recognize (likely a generic-only IList<T>); " +
                    "add an arm for it rather than coercing the whole collection into a single element.");

            if (listElementType.IsAssignableFrom(sourceType))
            {
                var list = (System.Collections.IList)System.Activator.CreateInstance(targetType)!;
                list.Add(value);
                return (list, null);
            }
            var (converted, convError) = TryConvert(value, listElementType, context);
            if (converted != null && listElementType.IsAssignableFrom(converted.GetType()))
            {
                var list = (System.Collections.IList)System.Activator.CreateInstance(targetType)!;
                list.Add(converted);
                return (list, null);
            }
            if (convError != null)
                return (null, convError);
        }

        // (string → path and string → path-backed reference fundamental (image) are
        // owned by path.Convert / image.Convert, dispatched via the hook above.)

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

        // (string → TimeSpan is owned by duration.Convert, dispatched via the hook above.)

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

        // (string/JsonElement/dict → goal.call is owned by GoalCall.Convert, dispatched via the hook above.)

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

    /// <summary>
    /// Prepends the binding slot (parameter/variable name) to an owning-type hook's
    /// error so an action-parameter bind failure still names where it failed — the hooks
    /// don't carry the slot, the door does. No-op when the slot is unknown or already named.
    /// </summary>
    private static error.Error WithSlot(error.Error e, string? targetName)
    {
        if (string.IsNullOrEmpty(targetName) || e.Message.Contains($"'{targetName}'"))
            return e;
        return new error.Error($"parameter '{targetName}': {e.Message}", e.Key, e.StatusCode)
        {
            FixSuggestion = e.FixSuggestion,
            Exception = e.Exception
        };
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
        // A value that is neither string nor collection: show its TYPE, never
        // ToString() it. A domain object's ToString() leaks the CLR type name as if
        // it were content and masks the real defect (an object reaching a text slot);
        // the type name alone is the honest, useful signal for a bind-failure message.
        return $"<{value.GetType().FullName}>";
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
