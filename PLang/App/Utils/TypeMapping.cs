using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using App;

namespace App.Utils;

/// <summary>
/// Maps between PLang type names and .NET types.
/// Provides a centralized place for type resolution.
/// </summary>
public static class TypeMapping
{
    private static readonly Dictionary<string, Type> NameToType = new(StringComparer.OrdinalIgnoreCase)
    {
        // Primitives
        ["string"] = typeof(string),
        ["text"] = typeof(string),
        ["int"] = typeof(int),
        ["integer"] = typeof(int),
        ["long"] = typeof(long),
        ["float"] = typeof(float),
        ["double"] = typeof(double),
        ["decimal"] = typeof(decimal),
        ["bool"] = typeof(bool),
        ["boolean"] = typeof(bool),
        ["datetime"] = typeof(DateTime),
        ["date"] = typeof(DateTime),
        ["time"] = typeof(TimeSpan),
        ["timespan"] = typeof(TimeSpan),
        ["guid"] = typeof(Guid),
        ["byte"] = typeof(byte),
        ["bytes"] = typeof(byte[]),

        // Collections
        ["list"] = typeof(List<object>),
        ["array"] = typeof(object[]),
        ["dictionary"] = typeof(Dictionary<string, object>),
        ["dict"] = typeof(Dictionary<string, object>),
        ["map"] = typeof(Dictionary<string, object>),
        ["object"] = typeof(object),
        ["dynamic"] = typeof(object),
        ["json"] = typeof(JsonNode),
        ["json[]"] = typeof(JsonArray),
        ["actor"] = typeof(App.Actor.@this),
        ["goal.call"] = typeof(App.Goals.Goal.GoalCall),
        ["tstring"] = typeof(App.Data.TString),
        ["translatable"] = typeof(App.Data.TString),
        ["path"] = typeof(App.FileSystem.Path),

        // Nullable types
        ["int?"] = typeof(int?),
        ["long?"] = typeof(long?),
        ["double?"] = typeof(double?),
        ["bool?"] = typeof(bool?),
        ["datetime?"] = typeof(DateTime?),
        ["guid?"] = typeof(Guid?),
    };

    private static readonly Dictionary<Type, string> TypeToName = new()
    {
        [typeof(string)] = "string",
        [typeof(int)] = "int",
        [typeof(long)] = "long",
        [typeof(float)] = "float",
        [typeof(double)] = "double",
        [typeof(decimal)] = "decimal",
        [typeof(bool)] = "bool",
        [typeof(DateTime)] = "datetime",
        [typeof(TimeSpan)] = "timespan",
        [typeof(Guid)] = "guid",
        [typeof(byte)] = "byte",
        [typeof(byte[])] = "bytes",
        [typeof(object)] = "object",
        [typeof(App.Goals.Goal.GoalCall)] = "goal.call",
        [typeof(App.Data.TString)] = "tstring",
        [typeof(App.FileSystem.Path)] = "path",
    };

    /// <summary>
    /// Registers a domain type for deserialization and type resolution.
    /// </summary>
    public static void Register(string plangName, Type clrType)
    {
        NameToType[plangName.ToLowerInvariant()] = clrType;
        TypeToName[clrType] = plangName.ToLowerInvariant();
    }

    private const int MaxGenericDepth = 20;

    /// <summary>
    /// Gets the .NET Type for a PLang type name.
    /// Handles generics (list&lt;string&gt;), dictionaries (dict&lt;K,V&gt;), nullable (int?), and MIME types.
    /// Depth-guarded against unbounded generic nesting.
    /// </summary>
    public static Type? GetType(string typeName) => GetType(typeName, 0);

    private static Type? GetType(string typeName, int depth)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return null;

        if (depth > MaxGenericDepth)
            return null;

        // Handle generic list syntax: list<string>
        if (typeName.StartsWith("list<", StringComparison.OrdinalIgnoreCase) && typeName.EndsWith(">"))
        {
            var innerTypeName = typeName[5..^1];
            var innerType = GetType(innerTypeName, depth + 1);
            return innerType != null ? typeof(List<>).MakeGenericType(innerType) : null;
        }

        // Handle generic dictionary syntax: dict<string,int>
        if ((typeName.StartsWith("dict<", StringComparison.OrdinalIgnoreCase) ||
             typeName.StartsWith("dictionary<", StringComparison.OrdinalIgnoreCase)) && typeName.EndsWith(">"))
        {
            var prefix = typeName.StartsWith("dict<", StringComparison.OrdinalIgnoreCase) ? 5 : 11;
            var inner = typeName[prefix..^1];
            var parts = inner.Split(',');
            if (parts.Length == 2)
            {
                var keyType = GetType(parts[0].Trim(), depth + 1);
                var valueType = GetType(parts[1].Trim(), depth + 1);
                if (keyType == null || valueType == null) return null;
                return typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
            }
        }

        if (NameToType.TryGetValue(typeName, out var type))
            return type;

        // MIME type resolution
        if (typeName.Contains('/'))
        {
            if (typeName.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
                return typeof(string);
            if (typeName.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
                typeName.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ||
                typeName.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
                return typeof(byte[]);
            if (typeName.Equals("application/json", StringComparison.OrdinalIgnoreCase))
                return typeof(object);
            if (typeName.Equals("application/plang-goal", StringComparison.OrdinalIgnoreCase))
                return typeof(App.Goals.Goal.@this);
            if (typeName.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
                return typeof(byte[]);
        }

        return null;
    }

    /// <summary>
    /// Gets the MIME type for a file extension.
    /// </summary>
    public static string GetMimeType(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return "application/octet-stream";

        return extension.ToLowerInvariant().TrimStart('.') switch
        {
            "md" => "text/markdown",
            "json" => "application/json",
            "xml" => "text/xml",
            "html" or "htm" => "text/html",
            "css" => "text/css",
            "js" => "text/javascript",
            "csv" => "text/csv",
            "yaml" or "yml" => "text/yaml",
            "txt" => "text/plain",
            "llm" => "text/plain",
            "goal" => "text/plain",
            "pr" => "application/plang-goal",
            "png" => "image/png",
            "jpg" or "jpeg" => "image/jpeg",
            "gif" => "image/gif",
            "svg" => "image/svg+xml",
            "webp" => "image/webp",
            "mp3" => "audio/mpeg",
            "wav" => "audio/wav",
            "mp4" => "video/mp4",
            "pdf" => "application/pdf",
            "zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    /// Gets the PLang type name for a .NET Type.
    /// </summary>
    public static string GetTypeName(Type type)
    {
        if (type == null)
            return "object";

        // Handle nullable types
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
        {
            return GetTypeName(underlying) + "?";
        }

        // Handle generic types
        if (type.IsGenericType)
        {
            var generic = type.GetGenericTypeDefinition();
            if (generic == typeof(Data.@this<>))
                return GetTypeName(type.GetGenericArguments()[0]);
            if (generic == typeof(List<>) || generic == typeof(IList<>))
            {
                return $"list<{GetTypeName(type.GetGenericArguments()[0])}>";
            }
            if (generic == typeof(Dictionary<,>) || generic == typeof(IDictionary<,>))
            {
                var args = type.GetGenericArguments();
                return $"dict<{GetTypeName(args[0])},{GetTypeName(args[1])}>";
            }
        }

        // Plain Data.@this (non-generic) — universal wrapper, maps to object
        if (type == typeof(Data.@this))
            return "object";

        // Handle arrays
        if (type.IsArray)
        {
            var elementType = type.GetElementType()!;
            if (elementType == typeof(byte))
                return "bytes";
            return $"list<{GetTypeName(elementType)}>";
        }

        if (TypeToName.TryGetValue(type, out var name))
            return name;

        // Check for ValidValues static property (convention for constrained types)
        // Return the lowercased type name — callers use GetValidValues() for the values
        var validValuesProp = type.GetProperty("ValidValues",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (validValuesProp != null && validValuesProp.PropertyType == typeof(string[]))
        {
            return StripGenericArity(type.Name).ToLowerInvariant();
        }

        return StripGenericArity(type.Name).ToLowerInvariant();
    }

    private static string StripGenericArity(string name)
    {
        var idx = name.IndexOf('`');
        return idx >= 0 ? name[..idx] : name;
    }

    /// <summary>
    /// Gets the valid values for a constrained type (e.g. Actor → ["user","service","system"]).
    /// Returns null if the type has no ValidValues convention property.
    /// </summary>
    public static string[]? GetValidValues(Type type)
    {
        // Unwrap nullable
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null) type = underlying;

        // Unwrap Data<T>
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Data.@this<>))
            type = type.GetGenericArguments()[0];

        // Enums: return all enum names
        if (type.IsEnum)
            return Enum.GetNames(type);

        // Convention: static ValidValues property
        var prop = type.GetProperty("ValidValues",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (prop != null && prop.PropertyType == typeof(string[]))
            return (string[])prop.GetValue(null)!;
        return null;
    }

    /// <summary>
    /// Determines if a type is considered a primitive type in PLang.
    /// </summary>
    public static bool IsPrimitive(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsPrimitive
            || underlying == typeof(string)
            || underlying == typeof(decimal)
            || underlying == typeof(DateTime)
            || underlying == typeof(DateTimeOffset)
            || underlying == typeof(TimeSpan)
            || underlying == typeof(Guid);
    }

    /// <summary>
    /// Attempts to convert a value to the specified type. Generic convenience overload.
    /// </summary>
    public static T? ConvertTo<T>(object? value) => (T?)ConvertTo(value, typeof(T));

    /// <summary>
    /// Attempts to convert a value to the specified type.
    /// Returns null on failure — use TryConvertTo for error details.
    /// </summary>
    public static object? ConvertTo(object? value, Type targetType)
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
    public static (object? Value, Errors.Error? Error) TryConvertTo(object? value, Type targetType,
        Actor.Context.@this? context = null)
    {
        if (value == null)
            return (targetType.IsValueType ? Activator.CreateInstance(targetType) : null, null);

        var sourceType = value.GetType();
        if (targetType.IsAssignableFrom(sourceType))
            return (value, null);

        // Data.@this is the universal value wrapper — any value can become Data
        if (targetType == typeof(Data.@this) && value is not Data.@this)
            return (new Data.@this("", value), null);

        // Handle nullable target types
        var underlying = Nullable.GetUnderlyingType(targetType);
        if (underlying != null)
            return TryConvertTo(value, underlying);

        // String → JsonNode: use ToJson() extension with fix-and-retry
        if (targetType == typeof(System.Text.Json.Nodes.JsonNode) && value is string jsonNodeStr)
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
            catch
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
                    catch (System.Text.Json.JsonException) { }
                }
            }
        }

        // List-like target: List<T> or types inheriting List<T>
        var listElementType = GetListElementType(targetType);
        if (listElementType != null)
        {
            // If source is already a list/collection, convert each element
            if (value is System.Collections.IList sourceList)
            {
                var targetList = (System.Collections.IList)Activator.CreateInstance(targetType)!;
                var errors = new List<Errors.Error>();
                for (int i = 0; i < sourceList.Count; i++)
                {
                    var (convertedItem, itemError) = TryConvertTo(sourceList[i], listElementType);
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

            // Single value → wrap into list
            if (listElementType.IsAssignableFrom(sourceType))
            {
                var list = (System.Collections.IList)Activator.CreateInstance(targetType)!;
                list.Add(value);
                return (list, null);
            }
            var (converted, convError) = TryConvertTo(value, listElementType);
            if (converted != null && listElementType.IsAssignableFrom(converted.GetType()))
            {
                var list = (System.Collections.IList)Activator.CreateInstance(targetType)!;
                list.Add(converted);
                return (list, null);
            }
            if (convError != null)
                return (null, convError);
        }

        // Handle IObject types — validated value types with string constructors
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
            catch (Exception ex)
            {
                var inner = ex.InnerException ?? ex;
                var validValues = GetValidValues(targetType);
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
                    for (int ci = 1; ci < ps.Length; ci++)
                        args[ci] = ps[ci].DefaultValue;
                    return (ctor.Invoke(args), null);
                }
                catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
                {
                    return (null, new Errors.Error(ex.InnerException?.Message ?? ex.Message, "ConstructorFailed", 400));
                }
            }
        }

        // Handle enum types
        if (targetType.IsEnum)
        {
            if (value is string s)
            {
                if (Enum.TryParse(targetType, s, ignoreCase: true, out var parsed))
                    return (parsed, null);
                return (null, new Errors.Error(
                    $"Cannot parse '{s}' as {targetType.Name}",
                    "EnumParseFailed", 400)
                    { FixSuggestion = $"Valid values: {string.Join(", ", Enum.GetNames(targetType))}" });
            }
            if (value.GetType().IsEnum)
                return (value, null);
            try { return (Enum.ToObject(targetType, value), null); }
            catch (ArgumentException) { return (null, new Errors.Error(
                $"Cannot convert {sourceType.Name} to enum {targetType.Name}",
                "EnumConversionFailed", 400)); }
        }

        // GoalCall: convert from string, JsonElement, or Dictionary (UnwrapJsonElement output)
        if (targetType == typeof(App.Goals.Goal.GoalCall))
        {

            if (value is string goalName)
                return (new App.Goals.Goal.GoalCall { Name = goalName }, null);
            if (value is System.Text.Json.JsonElement je)
            {
                try
                {
                    return (System.Text.Json.JsonSerializer.Deserialize<App.Goals.Goal.GoalCall>(
                        je.GetRawText(),
                        Json.CaseInsensitiveRead), null);
                }
                catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
                {
                    return (null, new Errors.Error(
                        $"Failed to deserialize GoalCall from JSON: {ex.Message}",
                        "GoalCallDeserializationFailed", 400));
                }
            }
            if (value is IDictionary<string, object?> dict)
            {
                var name = dict.TryGetValue("name", out var n) ? n?.ToString() ?? "" : "";
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

        // Use Convert for basic types
        if (IsPrimitive(targetType))
        {
            try
            {
                return (Convert.ChangeType(value, targetType), null);
            }
            catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
            {
                return (null, new Errors.Error(
                    $"Cannot convert '{value}' ({sourceType.Name}) to {targetType.Name}: {ex.Message}",
                    "PrimitiveConversionFailed", 400));
            }
        }

        // (String → JSON moved above list handling)

        // Complex types: dict/JsonElement/list → serialize to JSON → deserialize to target type
        if (value is IDictionary<string, object?> or System.Text.Json.JsonElement or System.Collections.IList)
        {
            string json = "";
            try
            {
                json = System.Text.Json.JsonSerializer.Serialize(value);
                var result = System.Text.Json.JsonSerializer.Deserialize(json, targetType, Json.CaseInsensitiveRead);
                return (result, null);
            }
            catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
            {
                // Extract byte position from error message and show JSON around it
                string jsonPreview;
                var posMatch = System.Text.RegularExpressions.Regex.Match(ex.Message, @"BytePositionInLine: (\d+)");
                if (posMatch.Success && int.TryParse(posMatch.Groups[1].Value, out var bytePos) && bytePos < json.Length)
                {
                    var start = Math.Max(0, bytePos - 100);
                    var end = Math.Min(json.Length, bytePos + 100);
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
    private static Type? GetListElementType(Type targetType)
    {
        // Direct List<T>
        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
            return targetType.GetGenericArguments()[0];

        // Inherits from List<T> (e.g., Actions : List<Action>)
        var baseType = targetType.BaseType;
        while (baseType != null)
        {
            if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(List<>))
                return baseType.GetGenericArguments()[0];
            baseType = baseType.BaseType;
        }

        return null;
    }


    /// <summary>
    /// Returns canonical builder type names (excludes aliases like "text"→"string").
    /// Keeps shortest name per CLR type, skips nullable variants.
    /// </summary>
    public static List<string> GetBuilderTypeNames()
    {
        var seen = new HashSet<Type>();
        var names = new List<string>();
        foreach (var kvp in NameToType)
        {
            if (kvp.Key.EndsWith("?")) continue;
            if (seen.Contains(kvp.Value)) continue;
            seen.Add(kvp.Value);

            var validValues = GetValidValues(kvp.Value);
            if (validValues != null)
                names.Add($"{kvp.Key}({string.Join("|", validValues)})");
            else
                names.Add(kvp.Key);
        }
        return names;
    }

    /// <summary>
    /// Returns schemas for complex types based on [LlmBuilder] attributes.
    /// Includes types registered in NameToType only. Use the overload with AppModules
    /// to auto-discover types from action parameters.
    /// </summary>
    public static Dictionary<string, string> GetComplexTypeSchemas()
    {
        return GetComplexTypeSchemas(null);
    }

    /// <summary>
    /// Returns schemas for complex types based on [LlmBuilder] attributes.
    /// When modules is provided, also discovers complex types used in action parameters
    /// (e.g., List&lt;LlmMessage&gt; → LlmMessage schema). No manual registration needed.
    /// </summary>
    public static Dictionary<string, string> GetComplexTypeSchemas(App.Modules.@this? modules)
    {
        var schemas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<System.Type>();

        // 1. Registered types from NameToType
        foreach (var kvp in NameToType)
        {
            var name = kvp.Key;
            var type = kvp.Value;
            if (name.EndsWith("?") || IsPrimitive(type) || type == typeof(object)) continue;
            if (type.IsArray || type.IsGenericType) continue;
            if (GetValidValues(type) != null) continue;

            TryAddSchema(schemas, seen, type, name);
        }

        // 2. Discover types from action parameters
        if (modules != null)
        {
            foreach (var ns in modules.Names)
            {
                foreach (var actionName in modules.GetActions(ns))
                {
                    var actionType = modules.GetActionType(ns, actionName);
                    if (actionType == null) continue;

                    foreach (var prop in actionType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (prop.Name == "EqualityContract" || prop.Name == "Context") continue;
                        var paramType = UnwrapType(prop.PropertyType);
                        if (paramType == null || IsPrimitive(paramType) || paramType == typeof(object)) continue;
                        if (seen.Contains(paramType)) continue;

                        var typeName = GetTypeName(paramType);
                        TryAddSchema(schemas, seen, paramType, typeName);
                    }
                }
            }
        }

        return schemas;
    }

    /// <summary>
    /// Unwraps generic wrappers (List&lt;T&gt;, Nullable&lt;T&gt;) to get the inner type.
    /// </summary>
    private static System.Type? UnwrapType(System.Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null) return UnwrapType(underlying);

        if (type.IsGenericType)
        {
            var generic = type.GetGenericTypeDefinition();
            if (generic == typeof(Data.@this<>))
                return UnwrapType(type.GetGenericArguments()[0]);
            if (generic == typeof(List<>) || generic == typeof(IList<>))
                return UnwrapType(type.GetGenericArguments()[0]);
            if (generic == typeof(Dictionary<,>) || generic == typeof(IDictionary<,>))
                return null; // dict values are too generic
        }

        if (type.IsArray)
            return UnwrapType(type.GetElementType()!);

        if (IsPrimitive(type)) return null;
        return type;
    }

    private static void TryAddSchema(Dictionary<string, string> schemas, HashSet<System.Type> seen, System.Type type, string name)
    {
        if (!seen.Add(type)) return;

        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.Name != "EqualityContract")
            .Where(p => Attribute.IsDefined(p, typeof(LlmBuilderAttribute)))
            .Where(p => !Attribute.IsDefined(p, typeof(JsonIgnoreAttribute)))
            .Select(p => $"{char.ToLower(p.Name[0]) + p.Name[1..]}: {GetTypeName(p.PropertyType)}");

        var propList = props.ToList();
        if (propList.Count > 0)
            schemas[name] = $"{{ {string.Join(", ", propList)} }}";
    }
}
