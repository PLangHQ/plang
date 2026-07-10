using System.Text.Json;
using System.Text.Json.Serialization;

namespace app.channel.serializer.json;

/// <summary>
/// The single STJ↔plang adapter. STJ consults it for any plang-typed value it
/// meets mid-graph (a <c>path</c> field nested deep in a CLR object that the
/// payload-level reader registry can't reach), and it routes that field to the
/// owning type's <c>Read</c> via <c>App.Type.Reader</c> / the renderer for
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

    // Only the abstract path.@this slot, matching exactly what the deleted
    // path.JsonConverter covered — concrete-subclass-typed slots fall through to
    // STJ default as they did before (no behavior change).
    public override bool CanConvert(System.Type typeToConvert)
        => typeToConvert == typeof(global::app.type.item.path.@this);

    public override JsonConverter? CreateConverter(System.Type typeToConvert, JsonSerializerOptions options)
        => new PathConverter(_context);

    /// <summary>
    /// The path arm. Read resolves the raw string directly via
    /// <c>path.@this.Resolve</c> so a mid-graph path field materializes the same way
    /// the payload-level path would; Write emits the portable wire string (identical to the
    /// deleted <c>path.JsonConverter.Write</c>).
    /// </summary>
    private sealed class PathConverter : JsonConverter<global::app.type.item.path.@this>
    {
        private readonly global::app.actor.context.@this? _context;
        public PathConverter(global::app.actor.context.@this? context) { _context = context; }

        public override global::app.type.item.path.@this? Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
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

            // The path type owns its construction — resolve the raw string directly (what
            // path/serializer/Default.Read did via the Readers.Of delegate; no reflection hop).
            // Born-with-context: the converter carries the actor scope (the Json serializer
            // always wires it), so the path resolves through the scheme registry with it.
            return global::app.type.item.path.@this.Resolve(raw!, _context!);
        }

        public override void Write(Utf8JsonWriter writer, global::app.type.item.path.@this value, JsonSerializerOptions options)
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
