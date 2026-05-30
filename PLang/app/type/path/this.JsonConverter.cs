using System.Text.Json;
using System.Text.Json.Serialization;

namespace app.type.path;

/// <summary>
/// STJ converter for <see cref="@this"/>. Wire shape is the path's portable
/// <c>Relative</c> string. The converter holds a Context provided at
/// construction — when <see cref="Read"/> fires, it routes through
/// <see cref="@this.Resolve(string, actor.context.@this)"/> so the resulting
/// Path is in the right scheme and fully Context-wired the moment it lands.
///
/// <para>Construction without Context (the default ctor) yields the "stub"
/// converter: Paths come out as bare file-scheme objects with Raw set and
/// Context=null. Used by the global Conversion fallback when no caller
/// supplied a Context — equivalent to today's perimeter parsing of raw
/// strings.</para>
///
/// <para>Per-Actor registration: each Actor's serializer chain bakes
/// <c>new JsonConverter(actor.Context)</c> into its options. <see cref="Conversion.TryConvertTo"/>
/// builds a one-shot Context-bound options bag per call when a Context was
/// passed in.</para>
/// </summary>
public sealed class JsonConverter : JsonConverter<@this>
{
    private readonly actor.context.@this? _context;

    /// <summary>Stub form — read produces a bare file-scheme Path with no Context. Used by the global Conversion fallback and any caller that hasn't wired an Actor.</summary>
    public JsonConverter() { _context = null; }

    /// <summary>Context-wired form — read routes through <see cref="@this.Resolve(string, actor.context.@this)"/>, landing scheme-correct Paths with the Actor's Context already bound.</summary>
    public JsonConverter(actor.context.@this context) { _context = context; }

    public override @this? Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Expected string for path, got {reader.TokenType}");

        var raw = reader.GetString();
        if (string.IsNullOrEmpty(raw)) return null;

        if (_context != null) return @this.Resolve(raw!, _context);
        // No Context — fall back to a file-scheme stub. Authorize callers will
        // explode on this Path; that's the contract.
        return new file.@this(raw!, context: null) { Raw = raw! };
    }

    public override void Write(Utf8JsonWriter writer, @this value, JsonSerializerOptions options)
    {
        // Serialize as the portable Relative form. Fall back to Raw or
        // Absolute when Context isn't wired (no root anchor).
        string? wire = null;
        if (value.Context != null)
        {
            try { wire = value.Relative; } catch { wire = null; }
        }
        wire ??= !string.IsNullOrEmpty(value.Raw) ? value.Raw : value.Absolute;
        writer.WriteStringValue(wire);
    }
}
