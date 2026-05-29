using System.Text.Json;
using System.Text.Json.Serialization;
using app;
using app.actor.context;

namespace app.type;

/// <summary>
/// PLang type entity. Value is a type string: "string", "long", "text/markdown", "image/jpeg", etc.
/// CLR type is derived on the fly via the per-App registry.
///
/// Promoted to <c>type/this.cs</c> in Stage 4 (singular-namespaces). Previously lived
/// at <c>app.type.@this</c>; that name remains as a global-using alias for transitional callers
/// (see <c>app/GlobalUsings.cs</c>) until the call-site sweep completes.
/// </summary>
public sealed class @this
{
    public string Value { get; }

    [JsonIgnore]
    internal actor.context.@this? Context { get; set; }

    public @this(string value) { Value = value; }

    /// <summary>
    /// Derive CLR type: navigate through context to App.Types, fall back to static TypeMapping.
    /// </summary>
    public System.Type? ClrType => Context?.App.Types.Clr(Value) ?? AppTypes.GetPrimitiveOrMime(Value);

    /// <summary>
    /// Kind of this type value (e.g. "image", "text"). Null for PLang type names like "string".
    /// </summary>
    public string? Kind => Context?.App.Formats.KindOf(Value);

    /// <summary>
    /// Whether content of this type benefits from compression.
    /// </summary>
    public bool Compressible => Kind != null && (Context?.App.Formats.Compressible(Kind) ?? false);

    public static @this String => new("string");
    public static @this Int => new("int");
    public static @this Long => new("long");
    public static @this Double => new("double");
    public static @this Bool => new("bool");
    public static @this DateTime => new("datetime");
    public static @this Object => new("object");

    /// <summary>Factory from MIME type (used by file handlers).</summary>
    public static @this FromMime(string mimeType) => new(mimeType);

    /// <summary>Factory from PLang type name.</summary>
    public static @this FromName(string typeName) => new(typeName);

    public override string ToString() => Value;

    /// <summary>
    /// Converts a raw string value to the appropriate object based on this type.
    /// Returns null if no conversion is needed or possible.
    /// </summary>
    public object? Convert(string raw)
    {
        return Value.ToLowerInvariant() switch
        {
            "json" => JsonSerializer.Deserialize<Dictionary<string, object?>>(raw,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }),
            _ => AppTypes.TryConvertTo(raw, ClrType ?? typeof(object)).Value
        };
    }

    // --- Stage 4 Entry-fold properties (computed lazily; sourced from BuildTypeEntries) ---

    // The entry is found on first access and cached per-instance.  Requires Context to
    // be set so we can reach the App's type registry.
    [JsonIgnore]
    private app.builder.type.Entry? _entry;
    [JsonIgnore]
    private bool _entryLoaded;

    [JsonIgnore]
    private app.builder.type.Entry? Entry
    {
        get
        {
            if (_entryLoaded) return _entry;
            if (Context?.App?.Type == null) return null;
            var entries = Context.App.Type.BuildTypeEntries(Context.App.Module);
            _entry = entries.FirstOrDefault(e => string.Equals(e.Name, Value, System.StringComparison.OrdinalIgnoreCase));
            _entryLoaded = true;
            return _entry;
        }
    }

    /// <summary>Record fields. Null when this type is not a record.</summary>
    public IReadOnlyList<app.builder.type.Field>? Fields => Entry?.Fields;

    /// <summary>Enum values. Null when this type is not an enum.</summary>
    public IReadOnlyList<string>? ValidValues => Entry?.Values;

    /// <summary>Scalar shape (the underlying primitive form, e.g. "string" for path).</summary>
    public string? Shape => Entry?.Shape;

    /// <summary>Constructor signature for scalar types.</summary>
    public string? ConstructorSignature => Entry?.ConstructorSignature;

    /// <summary>Canonical example from [PlangType(Example = ...)].</summary>
    public string? Example => Entry?.Example;

    /// <summary>Semantic description from [PlangType(Description = ...)].</summary>
    public string? Description => Entry?.Description;

    /// <summary>Developer-meaningful kind vocabulary.</summary>
    public IReadOnlyList<string>? Kinds => Entry?.Kinds;

    /// <summary>Path-scheme registry for the path entity. Null when this type is not path.</summary>
    public global::app.type.path.scheme.@this? Scheme
        => Value == "path" ? Context?.App?.Type?.Scheme : null;
}
