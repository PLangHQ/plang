using System.Text.Json;
using System.Text.Json.Serialization;

namespace app.channel.serializer.json;

/// <summary>
/// The single STJ↔plang adapter. STJ consults it for any plang-typed value it
/// meets mid-graph (a <c>path</c> field nested deep in a CLR object that the
/// payload-level reader registry can't reach), and it routes that field to the
/// owning type's <c>Read</c> via <c>App.Type.Readers</c> / the renderer for
/// <c>Write</c>. The read-side mirror of the single write-side <c>Wire</c>
/// converter.
///
/// <para>This replaces the format-coupled per-type value converters
/// (<c>path.JsonConverter</c>, …): a new wire format ships one such adapter and
/// reuses every type's <c>Read</c>/<c>Write</c> — types are never enumerated per
/// format at the registration sites. Built per-actor with the actor context
/// (exactly as <c>path.JsonConverter</c> was), so a path resolves scheme-correct
/// and fully wired the moment it lands; the context-less form yields the bare
/// file-scheme stub the global conversion fallback used.</para>
/// </summary>
public sealed class Converter : JsonConverterFactory
{
    private readonly global::app.actor.context.@this? _context;

    public Converter() { _context = null; }
    public Converter(global::app.actor.context.@this context) { _context = context; }

    // The context this serializer reads toward — the native dict/list converters
    // (attribute-instantiated, so they can't take it via ctor) read it off the
    // options to birth their parsed containers born-with-context.
    public global::app.actor.context.@this? Context => _context;

    /// <summary>The actor context carried by the <see cref="Converter"/> registered on
    /// <paramref name="options"/> — the channel json options always register one. Null
    /// only on a raw STJ options bag with no plang converter (no context to read toward).</summary>
    public static global::app.actor.context.@this? On(System.Text.Json.JsonSerializerOptions options)
    {
        foreach (var converter in options.Converters)
            if (converter is Converter plang) return plang.Context;
        return null;
    }

    // Only the abstract path.@this slot, matching exactly what the deleted
    // path.JsonConverter covered — concrete-subclass-typed slots fall through to
    // STJ default as they did before (no behavior change).
    public override bool CanConvert(System.Type typeToConvert)
        => typeToConvert == typeof(global::app.type.path.@this);

    public override JsonConverter? CreateConverter(System.Type typeToConvert, JsonSerializerOptions options)
        => new PathConverter(_context);

    /// <summary>
    /// The path arm. Read routes through the reader registry
    /// (<c>App.Type.Readers.Of("path", …)</c>) so a mid-graph path field
    /// materializes through the same <c>path.Read</c> the payload-level path
    /// would reach; Write emits the portable wire string (identical to the
    /// deleted <c>path.JsonConverter.Write</c>).
    /// </summary>
    private sealed class PathConverter : JsonConverter<global::app.type.path.@this>
    {
        private readonly global::app.actor.context.@this? _context;
        public PathConverter(global::app.actor.context.@this? context) { _context = context; }

        public override global::app.type.path.@this? Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return null;
            string? raw;
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                // The Data wire writes a path as its property bag {scheme, relative}
                // (domain types ride the wire as property bags); the CLR-graph writer
                // below emits the bare string. Read accepts both — the relative slot
                // is the location, resolved through the same reader as the string form.
                using var doc = JsonDocument.ParseValue(ref reader);
                raw = doc.RootElement.TryGetProperty("relative", out var rel) ? rel.GetString()
                    : doc.RootElement.TryGetProperty("raw", out var rw) ? rw.GetString()
                    : null;
            }
            else if (reader.TokenType == JsonTokenType.String)
            {
                raw = reader.GetString();
            }
            else
            {
                throw new JsonException($"Expected string or object for path, got {reader.TokenType}");
            }
            if (string.IsNullOrEmpty(raw)) return null;

            if (_context != null)
            {
                var read = _context.App.Type.Readers.Of("path", null);
                if (read != null)
                    return read(raw!, null, new global::app.type.reader.ReadContext(_context)) as global::app.type.path.@this;
            }
            // No context — bare file-scheme stub; Authorize callers explode on it
            // (the contract the old context-less converter kept).
            return new global::app.type.path.file.@this(raw!, context: null) { Raw = raw! };
        }

        public override void Write(Utf8JsonWriter writer, global::app.type.path.@this value, JsonSerializerOptions options)
        {
            string? wire = null;
            if (value.Context != null)
            {
                try { wire = value.Relative; }
                catch (System.Exception ex) when (ex is not (System.OutOfMemoryException or System.StackOverflowException))
                {
                    wire = null;
                }
            }
            wire ??= !string.IsNullOrEmpty(value.Raw) ? value.Raw : value.Absolute;
            writer.WriteStringValue(wire);
        }
    }
}
