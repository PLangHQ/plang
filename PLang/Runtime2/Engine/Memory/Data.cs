using System.Text.Json;
using System.Text.Json.Serialization;
using PLang.Attributes;
using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory.Navigators;
using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Utility;

namespace PLang.Runtime2.Engine.Memory;

/// <summary>
/// PLang type descriptor. Value is a type string: "string", "long", "text/markdown", "image/jpeg", etc.
/// CLR type is derived on the fly via TypeMapping.
/// </summary>
[System.ComponentModel.TypeConverter(typeof(PlangTypeConverter))]
public sealed class Type
{
    public string Value { get; }

    [JsonIgnore]
    internal PLangContext? Context { get; set; }

    public Type(string value) { Value = value; }

    /// <summary>
    /// Derive CLR type: navigate through context to Engine.Types, fall back to static TypeMapping.
    /// </summary>
    public System.Type? ClrType => Context?.Engine.Types.Clr(Value) ?? TypeMapping.GetType(Value);

    /// <summary>
    /// Kind of this type value (e.g. "image", "text"). Null for PLang type names like "string".
    /// </summary>
    public string? Kind => Context?.Engine.Types.KindOf(Value);

    /// <summary>
    /// Whether content of this type benefits from compression.
    /// </summary>
    public bool Compressible => Kind != null && (Context?.Engine.Types.Compressible(Kind) ?? false);

    public static Type String => new("string");
    public static Type Int => new("int");
    public static Type Long => new("long");
    public static Type Double => new("double");
    public static Type Bool => new("bool");
    public static Type DateTime => new("datetime");
    public static Type Object => new("object");

    /// <summary>
    /// Factory from MIME type (used by file handlers).
    /// </summary>
    public static Type FromMime(string mimeType) => new(mimeType);

    /// <summary>
    /// Factory from PLang type name.
    /// </summary>
    public static Type FromName(string typeName) => new(typeName);

    public override string ToString() => Value;
}

/// <summary>
/// Wraps a variable value in Runtime2 with metadata.
/// Name is the variable/parameter name, Value is the data accessed via %name%.
/// Also serves as the universal result type (replaces Return).
/// </summary>
public class Data
{
    private object? _value;
    private Type? _type;
    private PLangContext? _context;

    [JsonPropertyName("name")]
    public string Name { get; }

    [JsonIgnore]
    public PLangContext? Context
    {
        get => _context;
        set
        {
            _context = value;
            if (_type != null) _type.Context = value;
        }
    }

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

    // --- Error/Result support (replaces Return) ---

    /// <summary>
    /// When true, a before-event has handled this action/step/goal.
    /// The original execution should be skipped and this Data's Value used instead.
    /// </summary>
    [JsonIgnore]
    public bool Handled { get; set; }

    [JsonIgnore]
    public IError? Error { get; set; }

    [JsonIgnore]
    public List<Info>? Warnings { get; set; }

    [JsonIgnore]
    public bool Success => Error == null;

    public static implicit operator bool(Data d) => d.Success;

    [JsonConstructor]
    [Newtonsoft.Json.JsonConstructor]
    public Data(string name, object? value = null, Type? type = null, Data? parent = null)
    {
        Name = CleanName(name);
        _value = UnwrapJsonElement(value);
        _type = type;
        Parent = parent;
        Path = BuildPath(parent, Name);
        IsInitialized = _value != null;
        Created = System.DateTime.UtcNow;
        Updated = Created;
        Properties = new Properties();
    }

    [JsonPropertyName("value")]
    public virtual object? Value
    {
        get => _value;
        set
        {
            _value = UnwrapJsonElement(value);
            Updated = System.DateTime.UtcNow;
            IsInitialized = true;
            _type = null;
        }
    }

    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(TypeJsonConverter))]
    public Type? Type
    {
        get
        {
            if (_type != null) return _type;
            if (_value == null) return null;
            var typeName = _context?.Engine.Types.Name(_value.GetType())
                           ?? TypeMapping.GetTypeName(_value.GetType());
            var derived = new Type(typeName);
            derived.Context = _context;
            _type = derived;
            return _type;
        }
        set
        {
            _type = value;
            if (value != null && _context != null) value.Context = _context;
        }
    }

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
    public object? GetValue(System.Type targetType)
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
        child.Context = _context;

        if (string.IsNullOrEmpty(remaining))
            return child;

        return child.GetChild(remaining);
    }

    private object? GetChildValue(string key)
    {
        var val = Value;
        if (val == null) return null;
        return ValueNavigators.Navigate(val, key);
    }

    [JsonIgnore]
    public bool IsEmpty => !IsInitialized || _value == null ||
        (_value is string s && string.IsNullOrEmpty(s));

    public static Data Null(string name = "") => new(name, null);

    // --- Static helpers (replace Return helpers) ---

    public static Data Ok() => new("");
    public static Data Ok(object? value, Type? type = null) => new("", value, type);
    public static Data FromError(IError error) => new("") { Error = error };

    /// <summary>
    /// Merge: combines two Data results (logic from Return.Merge).
    /// Treats Value as List&lt;Data&gt;, merge by Name (replace-or-append).
    /// </summary>
    public Data Merge(Data other)
    {
        if (other.Value == null) return this;

        var myData = Value as List<Data> ?? new();
        var otherData = other.Value as List<Data> ?? new();

        foreach (var data in otherData)
        {
            var existing = myData.FindIndex(d => string.Equals(d.Name, data.Name, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
                myData[existing] = data;
            else
                myData.Add(data);
        }

        return new Data("") { Value = myData };
    }

    public override string ToString() =>
        Success ? _value?.ToString() ?? "(null)" : $"Error: {Error?.Message}";

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
                JsonValueKind.Object => UnwrapJsonObject(element),
                JsonValueKind.Array => UnwrapJsonArray(element),
                _ => element
            };
        }

        // Convert Newtonsoft JToken to CLR types (v1 runtime compatibility shim).
        // Detected by namespace so Runtime2 has no Newtonsoft import.
        if (value != null && value.GetType().Namespace == "Newtonsoft.Json.Linq")
        {
            return UnwrapNewtonsoftToken(value);
        }

        return value;
    }

    /// <summary>
    /// Converts a Newtonsoft JToken to plain CLR types without importing Newtonsoft.
    /// JValue → extract underlying CLR value via reflection.
    /// JObject/JArray → round-trip through JSON string → System.Text.Json.
    /// </summary>
    private static object? UnwrapNewtonsoftToken(object value)
    {
        var typeName = value.GetType().Name;

        // JValue holds a CLR primitive in its Value property
        if (typeName == "JValue")
        {
            var underlying = value.GetType().GetProperty("Value")?.GetValue(value);
            return underlying;
        }

        // JObject/JArray → serialize to JSON string, re-parse with System.Text.Json
        var json = value.ToString();
        if (string.IsNullOrEmpty(json)) return null;

        using var doc = JsonDocument.Parse(json);
        return UnwrapJsonElement(doc.RootElement);
    }

    private static Dictionary<string, object?> UnwrapJsonObject(JsonElement element)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in element.EnumerateObject())
        {
            dict[prop.Name] = UnwrapJsonElement(prop.Value);
        }
        return dict;
    }

    private static List<object?> UnwrapJsonArray(JsonElement element)
    {
        var list = new List<object?>();
        foreach (var item in element.EnumerateArray())
        {
            list.Add(UnwrapJsonElement(item));
        }
        return list;
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
/// Generic Data that carries a strongly-typed value.
/// Inherits from Data, so it satisfies Task&lt;Data&gt; in the interface chain.
/// </summary>
public class Data<T> : Data
{
    public new T? Value
    {
        get => base.Value is T typed ? typed : GetValue<T>();
        set => base.Value = value;
    }

    public Data(string name = "", T? value = default, Type? type = null, Data? parent = null)
        : base(name, value, type, parent) { }

    public static Data<T> Ok(T value, Type? type = null) => new("", value, type);
    public new static Data<T> FromError(IError error) => new() { Error = error };
}

/// <summary>
/// Dynamic Data that computes its value on access.
/// </summary>
public class DynamicData : Data
{
    private readonly Func<object?> _valueFactory;

    public DynamicData(string name, Func<object?> valueFactory, Type? type = null)
        : base(name, null, type)
    {
        _valueFactory = valueFactory;
    }

    public override object? Value => _valueFactory();
}
