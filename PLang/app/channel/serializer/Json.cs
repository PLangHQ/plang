using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace app.channel.serializer;

/// <summary>
/// JSON serializer using System.Text.Json with stream support.
/// Emits data.Value — JSON is the "value as JSON view" — the Data wrapper
/// stays on the C# side. The wrapped value's runtime type drives polymorphic
/// emit; nothing else is consulted.
/// </summary>
public sealed class Json : ISerializer
{
    public string Type => "application/json";
    public string Extension => ".json";

    private readonly JsonSerializerOptions _options;
    private readonly actor.context.@this _context;
    private readonly ConcurrentDictionary<View, Json> _viewCache = new();
    // The view this serializer writes in — ForView binds it so the IWriter path
    // (data.Output) filters [Out]/[Store] properties itself, the way the STJ filter
    // modifier used to. The base serializer writes the Out view.
    private readonly View _boundView;

    // Born-with-context: a serializer belongs to an actor, and an actor always has a context —
    // it's the context deserialized values are born on (Serialize sources it from the incoming
    // Data instead). There is no context-less serializer.
    public Json(actor.context.@this context) : this(null, context) { }

    private Json(JsonSerializerOptions? options, actor.context.@this context, View boundView = View.Out)
    {
        _context = context;
        _boundView = boundView;
        // When `options` is supplied (ForView / WithIndentation paths), it
        // already carries the PathJsonConverter via STJ's copy semantics —
        // don't allocate a fresh one we'd throw away. Only the `??` branch
        // builds default options, so the converter alloc lives inside it.
        _options = options ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { global::app.channel.serializer.filter.Sensitive.Strip }
            },
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
                // The native dict value type projects to a `{}` object under raw
                // STJ — without this it reflects its C# surface (Entries → Data …)
                // and cycles.
                new global::app.type.item.dict.Json(),
                new global::app.channel.serializer.json.Converter(context)
            }
        };
    }

    /// <summary>
    /// Returns a serializer that only includes properties tagged with the given view.
    /// </summary>
    public Json ForView(View view)
    {
        return _viewCache.GetOrAdd(view, v =>
        {
            var viewOptions = new JsonSerializerOptions(_options)
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver
                {
                    Modifiers = { global::app.channel.serializer.filter.Sensitive.Strip, global::app.channel.serializer.filter.View.For(v) }
                }
            };
            return new Json(viewOptions, _context, boundView: v);
        });
    }

    // The `view` param is the ForView-bound view (json binds it at construction, not per call);
    // the incoming param is honored when a caller drives the base serializer directly.
    public async Task<data.@this> SerializeAsync(Stream stream, data.@this data, global::app.View view = global::app.View.Out, CancellationToken cancellationToken = default)
    {
        try
        {
            // JSON is the "value as JSON view" — the channel drives the writer and the value
            // writes ITSELF (its own Output → Write(IWriter)); no STJ in the value path, no
            // per-type converters. Bare: emitsSchema:false skips the {name,type,…} envelope,
            // so the value rides alone (type inferred on read).
            View effectiveView = _boundView != global::app.View.Out ? _boundView : view;
            await using var utf8 = new Utf8JsonWriter(stream);
            var writer = new global::app.channel.serializer.json.Writer(
                utf8, _options, effectiveView, _context.App.Type.Renderer, emitsSchema: false);
            await data.Output(writer, effectiveView, _context);
            await utf8.FlushAsync(cancellationToken);
            return data.Context.Ok();
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException)
        {
            return data.Context.Error(new error.ServiceError(
                $"JSON serialize failed: {ex.Message}", "JsonSerializeError", 400) { Exception = ex });
        }
    }

    public async Task<data.@this> DeserializeAsync(Stream stream, global::app.View view = global::app.View.Out, CancellationToken cancellationToken = default)
    {
        try
        {
            if (stream.CanSeek && stream.Length == 0) return _context.Ok();
            var v = await JsonSerializer.DeserializeAsync<object?>(stream, _options, cancellationToken);
            return _context.Ok(v);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException)
        {
            return _context.Error(new error.ServiceError(
                $"JSON deserialize failed: {ex.Message}", "JsonDeserializeError", 400) { Exception = ex });
        }
    }

    public async Task<data.@this<T>> DeserializeAsync<T>(Stream stream, global::app.View view = global::app.View.Out, CancellationToken cancellationToken = default) where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
    {
        try
        {
            if (stream.CanSeek && stream.Length == 0) return _context.Ok<T>(default!);
            var v = await JsonSerializer.DeserializeAsync<T>(stream, _options, cancellationToken);
            return _context.Ok<T>(v!);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException)
        {
            return _context.Error<T>(new error.ServiceError(
                $"JSON deserialize failed: {ex.Message}", "JsonDeserializeError", 400) { Exception = ex });
        }
    }

    /// <summary>
    /// Reads a held value's JSON bytes into its plang type — makes a
    /// <see cref="global::app.channel.serializer.json.Reader"/> over the bytes and
    /// lets the type pull itself off it. A type with no reader yet borns its natural
    /// shape off the same pass.
    /// </summary>
    public global::app.type.item.@this Read(global::app.type.item.source source, global::app.type.reader.ReadContext ctx)
    {
        var type = source.Mint();
        var typeReader = ctx.Context.App.Type.Reader.Reader(type.Name, type.Kind?.Name, ctx.Context);
        byte[] bytes = source.Raw as byte[] ?? Encoding.UTF8.GetBytes(source.Raw.ToString() ?? "");
        var utf8 = new Utf8JsonReader(bytes);
        utf8.Read();
        var reader = new global::app.channel.serializer.json.Reader(utf8);
        return typeReader.Read(ref reader, type.Kind?.Name, ctx);
    }

    /// <summary>
    /// Creates a copy with indented output for pretty printing.
    /// </summary>
    public Json WithIndentation()
    {
        var newOptions = new JsonSerializerOptions(_options)
        {
            WriteIndented = true
        };
        return new Json(newOptions, _context);
    }

    /// <summary>
    /// Returns a new <see cref="Json"/> whose options carry the given converter.
    /// The original instance is not mutated — STJ's options copy ctor clones
    /// converters and other config.
    /// </summary>
    public Json WithConverter(JsonConverter converter)
    {
        var newOptions = new JsonSerializerOptions(_options);
        newOptions.Converters.Add(converter);
        return new Json(newOptions, _context);
    }

    /// <summary>
    /// Returns a new <see cref="Json"/> whose <see cref="JsonSerializerOptions.TypeInfoResolver"/>
    /// chains the given modifier onto the existing resolver. The original instance
    /// is not mutated.
    /// </summary>
    public Json WithModifier(Action<JsonTypeInfo> modifier)
    {
        var newOptions = new JsonSerializerOptions(_options);
        var existing = newOptions.TypeInfoResolver as DefaultJsonTypeInfoResolver
                       ?? new DefaultJsonTypeInfoResolver();
        var resolver = new DefaultJsonTypeInfoResolver();
        foreach (var m in existing.Modifiers) resolver.Modifiers.Add(m);
        resolver.Modifiers.Add(modifier);
        newOptions.TypeInfoResolver = resolver;
        return new Json(newOptions, _context);
    }

    /// <summary>
    /// Returns a new <see cref="Json"/> with the <see cref="global::app.channel.serializer.filter.Transport.ForInbound"/>
    /// modifier composed onto its resolver. Counterpart of the merged plang
    /// serializer's outbound chain.
    /// </summary>
    public Json ForInbound() => WithModifier(global::app.channel.serializer.filter.Transport.ForInbound);

    /// <summary>
    /// Internal accessor for the raw STJ options. Used by canonicalization
    /// code that needs to serialize through the same options the wire writer
    /// uses (so hashed-bytes ≡ wire-bytes).
    /// </summary>
    internal JsonSerializerOptions RawOptions => _options;
}
