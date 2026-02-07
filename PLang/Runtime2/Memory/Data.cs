using System.Text.Json;
using System.Text.Json.Serialization;
using PLang.Attributes;
using PLang.Runtime2.Utility;

namespace PLang.Runtime2.Memory;

/// <summary>
/// Type information for Data.
/// </summary>
public sealed class TypeInfo
{
    public string Name { get; }
    public Type ClrType { get; }
    public bool IsNullable { get; }
    public bool IsList { get; }
    public bool IsDictionary { get; }
    public TypeInfo? ElementType { get; }

    public TypeInfo(Type clrType)
    {
        ClrType = clrType;
        Name = TypeMapping.GetTypeName(clrType);
        IsNullable = Nullable.GetUnderlyingType(clrType) != null || !clrType.IsValueType;
        IsList = clrType.IsArray || (clrType.IsGenericType &&
            (clrType.GetGenericTypeDefinition() == typeof(List<>) ||
             clrType.GetGenericTypeDefinition() == typeof(IList<>)));
        IsDictionary = clrType.IsGenericType &&
            (clrType.GetGenericTypeDefinition() == typeof(Dictionary<,>) ||
             clrType.GetGenericTypeDefinition() == typeof(IDictionary<,>));

        if (IsList && clrType.IsGenericType)
        {
            ElementType = new TypeInfo(clrType.GetGenericArguments()[0]);
        }
        else if (clrType.IsArray)
        {
            ElementType = new TypeInfo(clrType.GetElementType()!);
        }
    }

    public static TypeInfo FromName(string typeName)
    {
        var type = TypeMapping.GetType(typeName) ?? typeof(object);
        return new TypeInfo(type);
    }

    public static TypeInfo String => new(typeof(string));
    public static TypeInfo Int => new(typeof(int));
    public static TypeInfo Long => new(typeof(long));
    public static TypeInfo Double => new(typeof(double));
    public static TypeInfo Bool => new(typeof(bool));
    public static TypeInfo DateTime => new(typeof(DateTime));
    public static TypeInfo Object => new(typeof(object));

    public override string ToString() => Name;
}

/// <summary>
/// Wraps a variable value in Runtime2 with metadata.
/// Name is the variable/parameter name, Value is the data accessed via %name%.
/// </summary>
public class Data
{
    private object? _value;
    private TypeInfo? _typeInfo;

    [JsonPropertyName("name")]
    public string Name { get; }

    [JsonIgnore]
    public string Path { get; }

    [JsonIgnore]
    [LlmIgnore]
    public Data? Parent { get; }

    [JsonIgnore]
    public bool IsInitialized { get; private set; }

    [JsonIgnore]
    public DateTime Created { get; }

    [JsonIgnore]
    public DateTime Updated { get; private set; }

    [JsonIgnore]
    [LlmIgnore]
    public Properties Properties { get; set; }

    [JsonConstructor]
    public Data(string name, object? value = null, TypeInfo? typeInfo = null, Data? parent = null)
    {
        Name = CleanName(name);
        _value = UnwrapJsonElement(value);
        _typeInfo = typeInfo ?? (_value != null ? new TypeInfo(_value.GetType()) : null);
        Parent = parent;
        Path = BuildPath(parent, Name);
        IsInitialized = _value != null;
        Created = DateTime.UtcNow;
        Updated = Created;
        Properties = new Properties();
    }

    [JsonPropertyName("value")]
    public object? Value
    {
        get => _value;
        set
        {
            _value = UnwrapJsonElement(value);
            Updated = DateTime.UtcNow;
            IsInitialized = true;
            if (_value != null && _typeInfo == null)
            {
                _typeInfo = new TypeInfo(_value.GetType());
            }
        }
    }

    [JsonIgnore]
    public TypeInfo? TypeInfo
    {
        get => _typeInfo;
        set => _typeInfo = value;
    }

    [JsonIgnore]
    public Type? ClrType => _typeInfo?.ClrType;

    /// <summary>
    /// Gets the value cast to the specified type.
    /// </summary>
    public T? GetValue<T>()
    {
        if (_value is T typed)
            return typed;

        var converted = TypeMapping.ConvertTo(_value, typeof(T));
        if (converted is T result)
            return result;

        return default;
    }

    /// <summary>
    /// Gets the value converted to the specified type.
    /// </summary>
    public object? GetValue(Type targetType)
    {
        if (_value == null)
            return null;

        if (targetType.IsAssignableFrom(_value.GetType()))
            return _value;

        return TypeMapping.ConvertTo(_value, targetType);
    }

    /// <summary>
    /// Gets a child value by path (dot notation or index).
    /// </summary>
    public Data? GetChild(string path)
    {
        if (string.IsNullOrEmpty(path))
            return this;

        // Handle dot notation
        var dotIndex = path.IndexOf('.');
        var bracketIndex = path.IndexOf('[');

        string segment;
        string remaining;

        if (dotIndex >= 0 && (bracketIndex < 0 || dotIndex < bracketIndex))
        {
            segment = path[..dotIndex];
            remaining = path[(dotIndex + 1)..];
        }
        else if (bracketIndex >= 0)
        {
            if (bracketIndex > 0)
            {
                segment = path[..bracketIndex];
                remaining = path[bracketIndex..];
            }
            else
            {
                var closeBracket = path.IndexOf(']');
                if (closeBracket < 0)
                    return null;
                segment = path[1..closeBracket];
                remaining = closeBracket + 1 < path.Length ? path[(closeBracket + 1)..].TrimStart('.') : "";
            }
        }
        else
        {
            segment = path;
            remaining = "";
        }

        // Get child value from current value
        var childValue = GetChildValue(segment);
        if (childValue == null)
            return null;

        var child = new Data(segment, childValue, parent: this);

        if (string.IsNullOrEmpty(remaining))
            return child;

        return child.GetChild(remaining);
    }

    private object? GetChildValue(string key)
    {
        if (_value == null)
            return null;

        // Handle dictionary-like objects
        if (_value is IDictionary<string, object?> dict)
        {
            return dict.TryGetValue(key, out var val) ? val : null;
        }

        if (_value is System.Collections.IDictionary idict)
        {
            return idict.Contains(key) ? idict[key] : null;
        }

        // Handle list/array indexing
        if (int.TryParse(key, out var index))
        {
            if (_value is System.Collections.IList list && index >= 0 && index < list.Count)
            {
                return list[index];
            }
        }

        // Handle object properties via reflection
        var prop = _value.GetType().GetProperty(key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        if (prop != null)
        {
            return prop.GetValue(_value);
        }

        return null;
    }

    [JsonIgnore]
    public bool IsEmpty => !IsInitialized || _value == null ||
        (_value is string s && string.IsNullOrEmpty(s));

    public static Data Null(string name = "") => new(name, null);

    public override string ToString() => _value?.ToString() ?? "(null)";

    private static object? UnwrapJsonElement(object? value)
    {
        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Undefined => null,
                _ => element
            };
        }
        return value;
    }

    private static string CleanName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;
        return name.Trim().TrimStart('%').TrimEnd('%');
    }

    private static string BuildPath(Data? parent, string name)
    {
        if (parent == null)
            return name;

        if (int.TryParse(name, out _))
            return $"{parent.Path}[{name}]";

        return $"{parent.Path}.{name}";
    }
}

/// <summary>
/// Dynamic Data that computes its value on access.
/// </summary>
public class DynamicData : Data
{
    private readonly Func<object?> _valueFactory;

    public DynamicData(string name, Func<object?> valueFactory, TypeInfo? typeInfo = null)
        : base(name, null, typeInfo)
    {
        _valueFactory = valueFactory;
    }

    public new object? Value => _valueFactory();
}
