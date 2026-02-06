namespace PLang.Runtime2.Utility;

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
    };

    /// <summary>
    /// Gets the .NET Type for a PLang type name.
    /// </summary>
    public static Type? GetType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return null;

        // Handle generic list syntax: list<string>
        if (typeName.StartsWith("list<", StringComparison.OrdinalIgnoreCase) && typeName.EndsWith(">"))
        {
            var innerTypeName = typeName[5..^1];
            var innerType = GetType(innerTypeName) ?? typeof(object);
            return typeof(List<>).MakeGenericType(innerType);
        }

        // Handle generic dictionary syntax: dict<string,int>
        if ((typeName.StartsWith("dict<", StringComparison.OrdinalIgnoreCase) ||
             typeName.StartsWith("dictionary<", StringComparison.OrdinalIgnoreCase)) && typeName.EndsWith(">"))
        {
            var prefix = typeName.StartsWith("dict<") ? 5 : 11;
            var inner = typeName[prefix..^1];
            var parts = inner.Split(',');
            if (parts.Length == 2)
            {
                var keyType = GetType(parts[0].Trim()) ?? typeof(string);
                var valueType = GetType(parts[1].Trim()) ?? typeof(object);
                return typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
            }
        }

        return NameToType.TryGetValue(typeName, out var type) ? type : null;
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

        return TypeToName.TryGetValue(type, out var name) ? name : type.Name.ToLowerInvariant();
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

        // Use Convert for basic types
        if (IsPrimitive(targetType))
        {
            try
            {
                return Convert.ChangeType(value, targetType);
            }
            catch
            {
                return null;
            }
        }

        return value;
    }
}
