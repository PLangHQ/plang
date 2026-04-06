using System.Text.Json;
using System.Text.Json.Serialization;
using Force.DeepCloner;
using PLang.Attributes;
using App.Engine;
using App.Engine.Channels.Serializers;
using App.Engine.Errors;
using App.Engine.Context;
using App.Engine.Utility;

namespace App.Engine.Variables;

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
/// Wraps a variable value in App with metadata.
/// Name is the variable/parameter name, Value is the data accessed via %name%.
/// Also serves as the universal result type (replaces Return).
/// Partial class — split by concern: Data.cs (core), Data.Result.cs, Data.Navigation.cs, Data.Envelope.cs.
/// </summary>
public partial class Data
{
    private object? _value;
    private Type? _type;
    private PLangContext? _context;

    [JsonPropertyName("name")]
    public string Name { get; set; }

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

    [JsonConstructor]
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

    /// <summary>
    /// Updates _value without triggering Value setter side effects (no type clearing, no unwrap).
    /// Used by RehydrateNestedData to replace a dictionary with a reconstructed Data object
    /// without losing the outer Type.
    /// </summary>
    private void SetValueDirect(object? value)
    {
        _value = value;
        Updated = System.DateTime.UtcNow;
        IsInitialized = true;
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

    [JsonIgnore]
    public bool IsEmpty => !IsInitialized || _value == null ||
        (_value is string s && string.IsNullOrEmpty(s));

    public static Data Null(string name = "") => new(name, null);

    /// <summary>
    /// Creates a deep clone of this Data. Value is deep-cloned, metadata is preserved.
    /// The natural boolean meaning of this Data.
    /// Default: IsInitialized. Subtypes override (e.g., Path → Exists).
    /// </summary>
    public virtual bool ToBoolean() => IsInitialized;

    /// <summary>
    /// Virtual so subclasses (DataList) can override with proper cloning.
    /// SettingsVariable and DynamicData should not be cloned — they are stateless/factory-based.
    /// </summary>
    public virtual Data Clone()
    {
        var clonedValue = _value.DeepClone();
        var clone = new Data(Name, clonedValue, _type)
        {
            Error = Error,
            Handled = Handled,
            Warnings = Warnings != null ? new List<Info>(Warnings) : null,
            Signature = Signature,
            Properties = Properties.Clone()
        };
        clone.Context = _context;
        return clone;
    }

    public override string ToString() =>
        Success ? _value?.ToString() ?? "(null)" : $"Error: {Error?.Message}";

    private const int MaxJsonDepth = 128;

    internal static object? UnwrapJsonElement(object? value, int depth = 0)
    {
        if (depth > MaxJsonDepth)
            throw new InvalidOperationException($"JSON nesting exceeds maximum depth ({MaxJsonDepth})");

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => UnwrapJsonNumber(element),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Undefined => null,
                JsonValueKind.Object => UnwrapJsonObject(element, depth),
                JsonValueKind.Array => UnwrapJsonArray(element, depth),
                _ => element
            };
        }

        // Convert Newtonsoft JToken to CLR types (v1 runtime compatibility shim).
        // Detected by namespace so App has no Newtonsoft import.
        if (value != null && value.GetType().Namespace == "Newtonsoft.Json.Linq")
        {
            return UnwrapNewtonsoftToken(value, depth);
        }

        return value;
    }

    /// <summary>
    /// Converts a Newtonsoft JToken to plain CLR types without importing Newtonsoft.
    /// JValue → extract underlying CLR value via reflection.
    /// JObject/JArray → round-trip through JSON string → System.Text.Json.
    /// </summary>
    private static object? UnwrapNewtonsoftToken(object value, int depth)
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
        return UnwrapJsonElement(doc.RootElement, depth);
    }

    private static Dictionary<string, object?> UnwrapJsonObject(JsonElement element, int depth)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in element.EnumerateObject())
        {
            dict[prop.Name] = UnwrapJsonElement(prop.Value, depth + 1);
        }
        return dict;
    }

    private static List<object?> UnwrapJsonArray(JsonElement element, int depth)
    {
        var list = new List<object?>();
        foreach (var item in element.EnumerateArray())
        {
            list.Add(UnwrapJsonElement(item, depth + 1));
        }
        return list;
    }

    private static object UnwrapJsonNumber(JsonElement element)
    {
        if (element.TryGetInt64(out var l)) return l;
        if (element.TryGetDecimal(out var d)) return d;
        return element.GetDouble();
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

/// <summary>
/// Typed list that carries error state. Extends Data so it can be returned from handlers.
/// On success, use as a list directly. On error, check Success/Error.
/// </summary>
public class DataList<T> : Data, IList<T>
{
    private readonly List<T> _items = new();

    public DataList(string name = "") : base(name) { }

    public static DataList<T> FromError(IError error) => new() { Error = error };

    // IList<T>
    public T this[int index] { get => _items[index]; set => _items[index] = value; }
    public int Count => _items.Count;
    public bool IsReadOnly => false;
    public void Add(T item) => _items.Add(item);
    public void Clear() => _items.Clear();
    public bool Contains(T item) => _items.Contains(item);
    public void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
    public int IndexOf(T item) => _items.IndexOf(item);
    public void Insert(int index, T item) => _items.Insert(index, item);
    public bool Remove(T item) => _items.Remove(item);
    public void RemoveAt(int index) => _items.RemoveAt(index);
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    // List helpers
    public T? Find(Predicate<T> match) => _items.Find(match);
    public bool Exists(Predicate<T> match) => _items.Exists(match);
    public List<T> Where(Func<T, bool> predicate) => _items.Where(predicate).ToList();

    /// <summary>
    /// Creates an independent copy with its own item list.
    /// </summary>
    public override Data Clone()
    {
        var clone = new DataList<T>(Name);
        foreach (var item in _items)
            clone._items.Add(item);
        clone.Error = Error;
        clone.Handled = Handled;
        clone.Warnings = Warnings != null ? new List<Info>(Warnings) : null;
        clone.Signature = Signature;
        clone.Properties = Properties.Clone();
        clone.Context = Context;
        return clone;
    }
}
