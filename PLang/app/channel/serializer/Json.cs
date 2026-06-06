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
    private readonly ConcurrentDictionary<View, Json> _viewCache = new();

    public Json(JsonSerializerOptions? options = null) : this(options, null) { }

    public Json(actor.context.@this? context) : this(null, context) { }

    private Json(JsonSerializerOptions? options, actor.context.@this? context)
    {
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
                new global::app.type.dict.Json(),
                context != null
                    ? new global::app.channel.serializer.json.Converter(context)
                    : new global::app.channel.serializer.json.Converter()
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
            return new Json(viewOptions);
        });
    }

    public async Task<data.@this> SerializeAsync(Stream stream, data.@this data, CancellationToken cancellationToken = default)
    {
        try
        {
            // Materialize lazy reference fundamentals (image bytes) above the
            // STJ converter wall — the sync renderers below cannot await.
            var loadError = await data.Load();
            if (loadError != null) return loadError;
            var value = data.Value;
            if (value == null)
            {
                await stream.WriteAsync("null"u8.ToArray(), cancellationToken);
                return global::app.data.@this.Ok();
            }
            await JsonSerializer.SerializeAsync(stream, value, value.GetType(), _options, cancellationToken);
            await stream.WriteAsync(Encoding.UTF8.GetBytes(Environment.NewLine), cancellationToken);
            return global::app.data.@this.Ok();
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException)
        {
            return global::app.data.@this.FromError(new error.ServiceError(
                $"JSON serialize failed: {ex.Message}", "JsonSerializeError", 400) { Exception = ex });
        }
    }

    public async Task<data.@this> DeserializeAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        try
        {
            if (stream.CanSeek && stream.Length == 0) return global::app.data.@this.Ok();
            var v = await JsonSerializer.DeserializeAsync<object?>(stream, _options, cancellationToken);
            return global::app.data.@this.Ok(v);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException)
        {
            return global::app.data.@this.FromError(new error.ServiceError(
                $"JSON deserialize failed: {ex.Message}", "JsonDeserializeError", 400) { Exception = ex });
        }
    }

    public async Task<data.@this<T>> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
    {
        try
        {
            if (stream.CanSeek && stream.Length == 0) return global::app.data.@this<T>.Ok(default!);
            var v = await JsonSerializer.DeserializeAsync<T>(stream, _options, cancellationToken);
            return global::app.data.@this<T>.Ok(v!);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException)
        {
            return global::app.data.@this<T>.FromError(new error.ServiceError(
                $"JSON deserialize failed: {ex.Message}", "JsonDeserializeError", 400) { Exception = ex });
        }
    }

    public data.@this<global::app.type.text.@this> Serialize(data.@this data)
    {
        try
        {
            var value = data.Value;
            if (value == null) return global::app.data.@this<global::app.type.text.@this>.Ok("null");
            return global::app.data.@this<global::app.type.text.@this>.Ok(JsonSerializer.Serialize(value, value.GetType(), _options));
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return global::app.data.@this<global::app.type.text.@this>.FromError(new error.ServiceError(
                $"JSON serialize failed: {ex.Message}", "JsonSerializeError", 400) { Exception = ex });
        }
    }

    public data.@this Deserialize(string s)
    {
        try
        {
            if (string.IsNullOrEmpty(s) || s == "null") return global::app.data.@this.Ok();
            return global::app.data.@this.Ok(JsonSerializer.Deserialize<object?>(s, _options));
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return global::app.data.@this.FromError(new error.ServiceError(
                $"JSON deserialize failed: {ex.Message}", "JsonDeserializeError", 400) { Exception = ex });
        }
    }

    public data.@this<T> Deserialize<T>(string s)
    {
        try
        {
            if (string.IsNullOrEmpty(s) || s == "null") return global::app.data.@this<T>.Ok(default!);
            return global::app.data.@this<T>.Ok(JsonSerializer.Deserialize<T>(s, _options)!);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return global::app.data.@this<T>.FromError(new error.ServiceError(
                $"JSON deserialize failed: {ex.Message}", "JsonDeserializeError", 400) { Exception = ex });
        }
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
        return new Json(newOptions);
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
        return new Json(newOptions);
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
        return new Json(newOptions);
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
