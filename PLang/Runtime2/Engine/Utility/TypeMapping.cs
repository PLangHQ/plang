using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PLang.Runtime2.Engine;

namespace PLang.Runtime2.Engine.Utility;

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
        ["actor"] = typeof(PLang.Runtime2.Engine.Context.Actor),
        ["goal.call"] = typeof(PLang.Runtime2.Engine.Goals.Goal.GoalCall),
        ["tstring"] = typeof(PLang.Runtime2.Engine.Memory.TString),
        ["translatable"] = typeof(PLang.Runtime2.Engine.Memory.TString),
        ["path"] = typeof(PLang.Runtime2.Engine.FileSystem.PathData),

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
        [typeof(PLang.Runtime2.Engine.Goals.Goal.GoalCall)] = "goal.call",
        [typeof(PLang.Runtime2.Engine.Memory.TString)] = "tstring",
        [typeof(PLang.Runtime2.Engine.FileSystem.PathData)] = "path",
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
            "pr" => "application/json",
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
    /// </summary>
    public static object? ConvertTo(object? value, Type targetType)
    {
        if (value == null)
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

        var sourceType = value.GetType();
        if (targetType.IsAssignableFrom(sourceType))
            return value;

        // Handle nullable target types
        var underlying = Nullable.GetUnderlyingType(targetType);
        if (underlying != null)
            return ConvertTo(value, underlying);

        // List<T> handling: element-wise conversion or single-value wrapping
        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
        {
            var elementType = targetType.GetGenericArguments()[0];

            // If source is already a list/collection, convert each element
            if (value is System.Collections.IList sourceList)
            {
                var targetList = (System.Collections.IList)Activator.CreateInstance(targetType)!;
                foreach (var item in sourceList)
                {
                    var convertedItem = ConvertTo(item, elementType);
                    if (convertedItem != null)
                        targetList.Add(convertedItem);
                }
                return targetList;
            }

            // Single value → wrap into List<T>
            if (elementType.IsAssignableFrom(sourceType))
            {
                var list = (System.Collections.IList)Activator.CreateInstance(targetType)!;
                list.Add(value);
                return list;
            }
            var converted = ConvertTo(value, elementType);
            if (converted != null && elementType.IsAssignableFrom(converted.GetType()))
            {
                var list = (System.Collections.IList)Activator.CreateInstance(targetType)!;
                list.Add(converted);
                return list;
            }
        }

        // Handle enum types
        if (targetType.IsEnum)
        {
            if (value is string s)
                return Enum.Parse(targetType, s, ignoreCase: true);
            if (value.GetType().IsEnum)
                return value;
            return Enum.ToObject(targetType, value);
        }

        // GoalCall: convert from string, JsonElement, or Dictionary (UnwrapJsonElement output)
        if (targetType == typeof(PLang.Runtime2.Engine.Goals.Goal.GoalCall))
        {
            if (value is string goalName)
                return new PLang.Runtime2.Engine.Goals.Goal.GoalCall { Name = goalName };
            if (value is System.Text.Json.JsonElement je)
            {
                try
                {
                    return System.Text.Json.JsonSerializer.Deserialize<PLang.Runtime2.Engine.Goals.Goal.GoalCall>(
                        je.GetRawText(),
                        Json.CaseInsensitiveRead);
                }
                catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException)) { return null; }
            }
            if (value is IDictionary<string, object?> dict)
            {
                var name = dict.TryGetValue("name", out var n) ? n?.ToString() ?? "" : "";
                List<Memory.Data>? parameters = null;
                if (dict.TryGetValue("parameters", out var p) && p is IList<object?> pList)
                {
                    parameters = pList
                        .OfType<IDictionary<string, object?>>()
                        .Select(d => new Memory.Data(
                            d.TryGetValue("name", out var dn) ? dn?.ToString() ?? "" : "",
                            d.TryGetValue("value", out var dv) ? dv : null))
                        .ToList();
                }
                return new PLang.Runtime2.Engine.Goals.Goal.GoalCall { Name = name, Parameters = parameters };
            }
        }

        // Use Convert for basic types
        if (IsPrimitive(targetType))
        {
            try
            {
                return Convert.ChangeType(value, targetType);
            }
            catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
            {
                return null;
            }
        }

        // Complex types: dict/JsonElement → serialize to JSON → deserialize to target type
        if (value is IDictionary<string, object?> or System.Text.Json.JsonElement)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(value);
                return System.Text.Json.JsonSerializer.Deserialize(json, targetType, Json.CaseInsensitiveRead);
            }
            catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException)) { return null; }
        }

        return value;
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
    /// Returns schemas for complex types (goal.call, etc.) based on [LlmBuilder] attributes.
    /// </summary>
    public static Dictionary<string, string> GetComplexTypeSchemas()
    {
        var schemas = new Dictionary<string, string>();
        foreach (var kvp in NameToType)
        {
            var name = kvp.Key;
            var type = kvp.Value;
            if (name.EndsWith("?") || IsPrimitive(type) || type == typeof(object)) continue;
            if (type.IsArray || type.IsGenericType) continue;
            if (GetValidValues(type) != null) continue;

            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.Name != "EqualityContract")
                .Where(p => Attribute.IsDefined(p, typeof(LlmBuilderAttribute)))
                .Where(p => !Attribute.IsDefined(p, typeof(JsonIgnoreAttribute)))
                .Select(p => $"{char.ToLower(p.Name[0]) + p.Name[1..]}: {GetTypeName(p.PropertyType)}");

            if (props.Any())
                schemas[name] = $"{{ {string.Join(", ", props)} }}";
        }
        return schemas;
    }
}
