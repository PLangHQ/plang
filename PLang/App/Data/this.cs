using System.Text.Json;
using System.Text.Json.Serialization;
using Force.DeepCloner;
using App.Attributes;
using App;
using App.Channels.Serializers;
using App.Errors;
using App.Actor.Context;
using App.Utils;

namespace App.Data;

/// <summary>
/// PLang type descriptor. Value is a type string: "string", "long", "text/markdown", "image/jpeg", etc.
/// CLR type is derived on the fly via TypeMapping.
/// </summary>
[System.ComponentModel.TypeConverter(typeof(PlangTypeConverter))]
public sealed class Type
{
    public string Value { get; }

    [JsonIgnore]
    internal Actor.Context.@this? Context { get; set; }

    public Type(string value) { Value = value; }

    /// <summary>
    /// Derive CLR type: navigate through context to App.Types, fall back to static TypeMapping.
    /// </summary>
    public System.Type? ClrType => Context?.App.Types.Clr(Value) ?? TypeMapping.GetType(Value);

    /// <summary>
    /// Kind of this type value (e.g. "image", "text"). Null for PLang type names like "string".
    /// </summary>
    public string? Kind => Context?.App.Types.KindOf(Value);

    /// <summary>
    /// Whether content of this type benefits from compression.
    /// </summary>
    public bool Compressible => Kind != null && (Context?.App.Types.Compressible(Kind) ?? false);

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

    /// <summary>
    /// Converts a raw string value to the appropriate object based on this type.
    /// Returns null if no conversion is needed or possible.
    /// Called lazily on first navigation into a string-typed Data.
    /// </summary>
    public object? Convert(string raw)
    {
        return Value.ToLowerInvariant() switch
        {
            "json" => JsonSerializer.Deserialize<Dictionary<string, object?>>(raw,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }),
            _ => TypeMapping.TryConvertTo(raw, ClrType ?? typeof(object)).Value
        };
    }
}

/// <summary>
/// Wraps a variable value in App with metadata.
/// Name is the variable/parameter name, Value is the data accessed via %name%.
/// Also serves as the universal result type (replaces Return).
/// Partial class — split by concern: Data.cs (core), Data.Result.cs, Data.Navigation.cs, Data.Envelope.cs.
/// </summary>
public partial class @this
{
    private object? _value;
    private Func<object?>? _valueFactory;
    private Type? _type;
    private Actor.Context.@this? _context;

    /// <summary>Fired by Variables.Set() when this Data is replaced — passes old and new Data.</summary>
    public event Action<@this, @this>? OnChange;

    /// <summary>Fired when variable is first created in the store — passes the Data.</summary>
    public event Action<@this>? OnCreate;

    /// <summary>Fired by Variables.Remove() before deletion — passes the Data.</summary>
    public event Action<@this>? OnDelete;

    /// <summary>Copies event handlers from another Data (used when replacing in variable store).</summary>
    public void CopyEventsFrom(@this other)
    {
        if (other.OnCreate != null) OnCreate += other.OnCreate;
        if (other.OnChange != null) OnChange += other.OnChange;
        if (other.OnDelete != null) OnDelete += other.OnDelete;
    }

    /// <summary>Fires the OnChange event. Called by Variables.Set() when replacing.</summary>
    public void FireOnChange(@this newData) => OnChange?.Invoke(this, newData);

    /// <summary>Fires the OnCreate event.</summary>
    public void FireOnCreate() => OnCreate?.Invoke(this);

    /// <summary>Fires the OnDelete event.</summary>
    public void FireOnDelete() => OnDelete?.Invoke(this);

    /// <summary>
    /// When true, Value resolves %variable% references on access.
    /// Set for .pr parameter Data — their values contain %var% references.
    /// Not set for variable.set Data — their values are already final.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool NeedsResolution { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonIgnore]
    public Actor.Context.@this? Context
    {
        get => _context;
        set
        {
            _context = value;
            if (_type != null) _type.Context = value;
            if (_value is modules.IContext contextual && value != null)
                contextual.Context = value;
        }
    }

    [JsonIgnore]
    public string Path { get; }

    [JsonIgnore]
    [LlmIgnore]
    public @this? Parent { get; }

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
    public @this(string name, object? value = null, Type? type = null, @this? parent = null)
    {
        Name = CleanName(name);
        _value = UnwrapJsonElement(value);
        _type = type;
        Parent = parent;
        Path = BuildPath(parent, Name);
        IsInitialized = true;
        Created = System.DateTime.UtcNow;
        Updated = Created;
        Properties = new Properties();
        if (parent != null)
            _context = parent._context;
    }

    [JsonPropertyName("value")]
    public virtual object? Value
    {
        get
        {
            if (_valueFactory != null)
            {
                _value = _valueFactory();
                _valueFactory = null;
            }
            if (NeedsResolution && _value != null && _context?.Variables != null
                && (_value is System.Collections.IList || _value is System.Collections.IDictionary))
                return _context.Variables.ResolveDeep(_value);
            return _value;
        }
        set
        {
            _value = UnwrapJsonElement(value);
            _valueFactory = null;
            Updated = System.DateTime.UtcNow;
            IsInitialized = true;
            _type = null;
            if (_value is modules.IContext contextual && _context != null)
                contextual.Context = _context;
        }
    }

    /// <summary>
    /// Sets a lazy value factory. Invoked on first Value access, then cached.
    /// </summary>
    public void SetValue(Func<object?> factory)
    {
        _valueFactory = factory;
        _value = null;
        IsInitialized = true;
    }

    /// <summary>
    /// Lazily converts the value based on its Type.
    /// Called on first navigation into the value — if the value is a string
    /// and the Type knows how to convert it, replaces the value with the converted object.
    /// Only converts once — subsequent accesses use the converted value directly.
    /// </summary>
    public void ConvertValue()
    {
        if (_value is not string raw || _type == null) return;
        var converted = _type.Convert(raw);
        if (converted != null)
            SetValueDirect(converted);
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
            var typeName = _context?.App.Types.Name(_value.GetType())
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
    /// Enumerates the inner value. If Value is enumerable, delegates to it.
    /// If Value is a single non-enumerable item, yields it as a one-element sequence.
    /// </summary>
    public System.Collections.IEnumerable AsEnumerable()
    {
        if (_value is System.Collections.IEnumerable enumerable and not string)
            return enumerable;

        // Single value — treat as a list of one
        if (_value != null)
            return new[] { _value };

        return Array.Empty<object>();
    }

    [JsonIgnore]
    public bool IsEmpty => !IsInitialized || _value == null ||
        (_value is string s && string.IsNullOrEmpty(s));

    /// <summary>Returns the raw stored value without triggering NeedsResolution or factory.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public object? RawValue => _value;

    public static @this Null(string name = "") => new(name, null);
    public static @this NotFound(string name = "") => new(name, null) { IsInitialized = false };
    public static @this Uninitialized(string name) => new(name, null) { IsInitialized = false };

    /// <summary>
    /// Converts this Data to a Data&lt;T&gt; by converting the inner Value.
    /// OBP: Data owns its own conversion — it has the Value, the Type, and the Context.
    /// Returns this if Value is already T. Otherwise uses TypeMapping.
    /// </summary>
    public @this<T> As<T>(Actor.Context.@this? context = null)
    {
        // Already a Data<T> with correct type — return directly
        if (this is @this<T> typed && typed.Value is T)
            return typed;

        // Value is already the right type — wrap
        if (Value is T already)
            return new @this<T>(Name, already, _type, Parent) { Context = context ?? _context };

        // Convert using TypeMapping
        var ctx = context ?? _context;

        // Context-resolvable types: if T has static Resolve(string, Context) and Value is string
        if (Value is string strVal && ctx != null)
        {
            var resolveMethod = typeof(T).GetMethod("Resolve",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null, new[] { typeof(string), typeof(Actor.Context.@this) }, null);
            if (resolveMethod != null)
            {
                var resolved = resolveMethod.Invoke(null, new object[] { strVal, ctx });
                if (resolved is T result)
                    return new @this<T>(Name, result, _type, Parent) { Context = ctx };
            }
        }

        var (converted, error) = TypeMapping.TryConvertTo(Value, typeof(T), ctx);
        if (error != null)
            return @this<T>.FromError(error);

        return new @this<T>(Name, (T?)converted, _type, Parent) { Context = ctx };
    }

    /// <summary>
    /// Creates a deep clone of this Data. Value is deep-cloned, metadata is preserved.
    /// The natural boolean meaning of this Data.
    /// Follows common language conventions: null, false, 0, "" are falsy. Everything else is truthy.
    /// </summary>
    public virtual bool ToBoolean()
    {
        if (!IsInitialized) return false;
        var val = Value;
        if (val == null) return false;
        if (val is bool b) return b;
        if (val is string s) return s.Length > 0;
        if (val is int i) return i != 0;
        if (val is long l) return l != 0;
        if (val is double d) return d != 0;
        if (val is float f) return f != 0;
        if (val is decimal dec) return dec != 0;
        if (val is short sh) return sh != 0;
        if (val is byte by) return by != 0;
        return true;
    }

    /// <summary>
    /// Virtual so subclasses can override with proper cloning.
    /// SettingsVariable and DynamicData should not be cloned — they are stateless/factory-based.
    /// </summary>
    /// <summary>
    /// Creates a new Data wrapper around the same value (no deep copy).
    /// Use when renaming — the value stays shared so mutations propagate.
    /// </summary>
    public @this ShallowClone()
    {
        var clone = new @this(Name, _value, _type)
        {
            Error = Error,
            Handled = Handled,
            Returned = Returned,
            ReturnDepth = ReturnDepth,
            Warnings = Warnings != null ? new List<Info>(Warnings) : null,
            Signature = Signature,
            Properties = Properties
        };
        clone.Context = _context;
        clone.NeedsResolution = NeedsResolution;
        return clone;
    }

    public virtual @this Clone()
    {
        var clonedValue = _value.DeepClone();
        var clone = new @this(Name, clonedValue, _type)
        {
            Error = Error,
            Handled = Handled,
            Returned = Returned,
            ReturnDepth = ReturnDepth,
            Warnings = Warnings != null ? new List<Info>(Warnings) : null,
            Signature = Signature,
            Properties = Properties.Clone()
        };
        clone.Context = _context;
        clone.NeedsResolution = NeedsResolution;
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

    private static string BuildPath(@this? parent, string name)
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
public class @this<T> : @this
{
    public new T? Value
    {
        get => base.Value is T typed ? typed : GetValue<T>();
        set => base.Value = value;
    }

    public @this(string name = "", T? value = default, Type? type = null, @this? parent = null)
        : base(name, value, type, parent) { }

    public static @this<T> Ok(T value, Type? type = null) => new("", value, type);
    public new static @this<T> FromError(IError error) => new() { Error = error };

    /// <summary>Allows direct assignment of T values to Data.@this&lt;T&gt; properties.</summary>
    public static implicit operator @this<T>(T value) => new("", value);
}

/// <summary>
/// Dynamic Data that computes its value on access.
/// </summary>
public class DynamicData : @this
{
    private readonly Func<object?> _valueFactory;

    public DynamicData(string name, Func<object?> valueFactory, Type? type = null)
        : base(name, null, type)
    {
        _valueFactory = valueFactory;
    }

    public override object? Value => _valueFactory();
}

