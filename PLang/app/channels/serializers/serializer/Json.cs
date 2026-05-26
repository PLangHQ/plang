using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace app.channels.serializers.serializer;

/// <summary>
/// JSON serializer using System.Text.Json with stream support.
/// </summary>
public sealed class Json : ISerializer
{
    public string ContentType => "application/json";
    public string FileExtension => ".json";

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
                Modifiers = { global::app.channels.serializers.filters.Sensitive.Strip }
            },
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
                context != null
                    ? new global::app.types.path.JsonConverter(context)
                    : new global::app.types.path.JsonConverter()
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
                    Modifiers = { global::app.channels.serializers.filters.Sensitive.Strip, global::app.channels.serializers.filters.View.For(v) }
                }
            };
            return new Json(viewOptions);
        });
    }

    public async Task SerializeAsync(Stream stream, object? value, Type? type = null, CancellationToken cancellationToken = default)
    {
        if (value == null)
        {
            await stream.WriteAsync("null"u8.ToArray(), cancellationToken);
            return;
        }

        await JsonSerializer.SerializeAsync(stream, value, type ?? value.GetType(), _options, cancellationToken);
        await stream.WriteAsync(Encoding.UTF8.GetBytes(Environment.NewLine), cancellationToken);
    }

    public async Task<object?> DeserializeAsync(Stream stream, Type type, CancellationToken cancellationToken = default)
    {
        if (stream.Length == 0)
            return null;

        return await JsonSerializer.DeserializeAsync(stream, type, _options, cancellationToken);
    }

    public async Task<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
    {
        if (stream.Length == 0)
            return default;

        return await JsonSerializer.DeserializeAsync<T>(stream, _options, cancellationToken);
    }

    public string Serialize(object? value, Type? type = null)
    {
        if (value == null)
            return "null";

        type ??= value.GetType();
        return JsonSerializer.Serialize(value, type, _options);
    }

    public object? Deserialize(string data, Type type)
    {
        if (string.IsNullOrEmpty(data) || data == "null")
            return null;

        return JsonSerializer.Deserialize(data, type, _options);
    }

    public T? Deserialize<T>(string data)
    {
        if (string.IsNullOrEmpty(data) || data == "null")
            return default;

        return JsonSerializer.Deserialize<T>(data, _options);
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
}
