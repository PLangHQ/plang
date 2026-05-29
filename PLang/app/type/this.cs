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
}
