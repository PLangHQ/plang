using System.Text.Json;
using System.Text.Json.Serialization;
using app;
using app.actor.context;

namespace app.type;

/// <summary>
/// PLang type entity. <see cref="Value"/> is the type name: "string", "long",
/// "text/markdown", "image/jpeg", etc.  The same entity carries the full
/// catalog knowledge — Fields, Values, Properties, Shape, ConstructorSignature,
/// Example, Description, Kinds, ClrType — folded in during Stage 4
/// (singular-namespaces) from the prior <c>app.builder.type.Entry</c> parallel
/// struct.  Both doors — <c>data.Type</c> and <c>app.Type[name]</c> — return
/// the same entity shape; <c>app.Type</c> resolves names through the registry
/// and stamps <c>Context</c>, while <c>type.list.@this.BuildTypeEntries</c>
/// walks the action catalog and populates the catalog properties at
/// construction.  Entities minted outside <c>BuildTypeEntries</c> lazily
/// resolve the catalog properties on first read by looking themselves up in
/// the registry's catalog walk.
///
/// No discriminator enum — the populated property set <em>is</em> the
/// discriminator: <see cref="Fields"/> non-null → record-shape; <see cref="Values"/>
/// non-null → enum-shape; <see cref="Shape"/> or <see cref="ConstructorSignature"/>
/// non-null → scalar-shape.
/// </summary>
public sealed class @this
{
    [JsonPropertyName("name")]
    public string Value { get; }

    [JsonIgnore]
    internal actor.context.@this? Context { get; set; }

    public @this(string value) { Value = value; }

    [JsonIgnore]
    public System.Type? ClrType
        => _clrType ?? Context?.App.Type.Clr(Value) ?? AppTypes.GetPrimitiveOrMime(Value);
    private System.Type? _clrType;

    /// <summary>Format kind for this type value (e.g. "image", "text"). Null for PLang type names like "string".</summary>
    [JsonIgnore]
    public string? Kind => Context?.App.Format.KindOf(Value);

    /// <summary>Whether content of this type benefits from compression.</summary>
    [JsonIgnore]
    public bool Compressible => Kind != null && (Context?.App.Format.Compressible(Kind) ?? false);

    public static @this String => new("string");
    public static @this Int => new("int");
    public static @this Long => new("long");
    public static @this Double => new("double");
    public static @this Bool => new("bool");
    public static @this DateTime => new("datetime");
    public static @this Object => new("object");

    public static @this FromMime(string mimeType) => new(mimeType);
    public static @this FromName(string typeName) => new(typeName);

    public override string ToString() => Value;

    public object? Convert(string raw)
    {
        return Value.ToLowerInvariant() switch
        {
            "json" => JsonSerializer.Deserialize<Dictionary<string, object?>>(raw,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }),
            _ => AppTypes.TryConvertTo(raw, ClrType ?? typeof(object)).Value
        };
    }

    // --- Catalog properties (init-only; promoted lazily) ---

    private IReadOnlyList<Field>? _fields;
    private IReadOnlyList<string>? _values;
    private IReadOnlyList<Field>? _properties;
    private string? _shape;
    private string? _constructorSignature;
    private string? _example;
    private string? _description;
    private IReadOnlyList<string>? _kinds;
    private bool _foldLoaded;

    /// <summary>Record fields. Non-null marks this as a record-shape type.</summary>
    public IReadOnlyList<Field>? Fields { get => Promote()._fields; init => _fields = value; }

    /// <summary>Enum values. Non-null marks this as an enum-shape type.</summary>
    public IReadOnlyList<string>? Values { get => Promote()._values; init => _values = value; }

    /// <summary>Read-only navigation properties for scalar types.</summary>
    public IReadOnlyList<Field>? Properties { get => Promote()._properties; init => _properties = value; }

    /// <summary>Scalar wire shape (the underlying primitive form, e.g. "string" for path).</summary>
    public string? Shape { get => Promote()._shape; init => _shape = value; }

    /// <summary>Constructor signature for scalar types (<c>"name: shape"</c>).</summary>
    public string? ConstructorSignature { get => Promote()._constructorSignature; init => _constructorSignature = value; }

    /// <summary>Canonical example from a static <c>Example</c> property on the type.</summary>
    public string? Example { get => Promote()._example; init => _example = value; }

    /// <summary>Semantic description from a static <c>Description</c> property on the type.</summary>
    public string? Description { get => Promote()._description; init => _description = value; }

    /// <summary>Developer-meaningful kind vocabulary from a static <c>Kinds</c> property.</summary>
    public IReadOnlyList<string>? Kinds { get => Promote()._kinds; init => _kinds = value; }

    /// <summary>Alias for <see cref="Values"/> — enum members the LLM may emit.</summary>
    [JsonIgnore]
    public IReadOnlyList<string>? ValidValues => Values;

    /// <summary>Path-scheme registry for the path entity. Null when this type is not path.</summary>
    [JsonIgnore]
    public global::app.type.path.scheme.@this? Scheme
        => Value == "path" ? Context?.App?.Type?.Scheme : null;

    // Construct with a stamped ClrType (used by BuildTypeEntries; spares the registry round-trip).
    internal @this(string value, System.Type? clrType) : this(value) { _clrType = clrType; }

    private @this Promote()
    {
        if (_foldLoaded) return this;
        // Already populated by an init-only setter — no promotion needed.
        if (_fields != null || _values != null || _properties != null
            || _shape != null || _constructorSignature != null
            || _example != null || _description != null || _kinds != null)
        {
            _foldLoaded = true;
            return this;
        }
        _foldLoaded = true;
        if (Context?.App?.Type == null) return this;
        var entries = Context.App.Type.BuildTypeEntries(Context.App.Module);
        var match = entries.FirstOrDefault(e => string.Equals(e.Value, Value, System.StringComparison.OrdinalIgnoreCase));
        if (match == null) return this;
        _fields = match._fields;
        _values = match._values;
        _properties = match._properties;
        _shape = match._shape;
        _constructorSignature = match._constructorSignature;
        _example = match._example;
        _description = match._description;
        _kinds = match._kinds;
        if (_clrType == null) _clrType = match._clrType;
        return this;
    }
}
