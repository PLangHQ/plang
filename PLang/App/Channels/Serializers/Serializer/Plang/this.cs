using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace App.Channels.Serializers.Serializer.Plang;

/// <summary>
/// Serializes the full Data envelope for PLang-to-PLang transport.
/// Content type: application/plang. Includes name, value, type, properties, and signature.
/// External formats (application/json, text/plain) serialize just the value —
/// this serializer preserves the complete Data structure for inter-app communication.
/// </summary>
public sealed class @this : ISerializer
{
    public string ContentType => "application/plang";
    public string FileExtension => ".plang";

    private readonly JsonSerializerOptions _serializeOptions;
    private readonly JsonSerializerOptions _deserializeOptions;

    public @this()
    {
        _serializeOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase), new global::App.Data.Json() },
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { global::App.Channels.Serializers.Filters.Transport.ForOutbound }
            }
        };

        _deserializeOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase), new global::App.Data.Json() },
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { global::App.Channels.Serializers.Filters.Transport.ForInbound }
            }
        };
    }

    public async Task SerializeAsync(Stream stream, object? value, Type? type = null, CancellationToken cancellationToken = default)
    {
        if (value == null)
        {
            await stream.WriteAsync("null"u8.ToArray(), cancellationToken);
            return;
        }

        await JsonSerializer.SerializeAsync(stream, value, type ?? value.GetType(), _serializeOptions, cancellationToken);
    }

    public async Task<object?> DeserializeAsync(Stream stream, Type type, CancellationToken cancellationToken = default)
    {
        if (stream.Length == 0) return null;

        return await JsonSerializer.DeserializeAsync(stream, type, _deserializeOptions, cancellationToken);
    }

    public async Task<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
    {
        if (stream.Length == 0) return default;

        return await JsonSerializer.DeserializeAsync<T>(stream, _deserializeOptions, cancellationToken);
    }

    public string Serialize(object? value, Type? type = null)
    {
        if (value == null) return "null";

        return JsonSerializer.Serialize(value, type ?? value.GetType(), _serializeOptions);
    }

    public object? Deserialize(string data, Type type)
    {
        if (string.IsNullOrEmpty(data) || data == "null") return null;

        return JsonSerializer.Deserialize(data, type, _deserializeOptions);
    }

    public T? Deserialize<T>(string data)
    {
        if (string.IsNullOrEmpty(data) || data == "null") return default;

        return JsonSerializer.Deserialize<T>(data, _deserializeOptions);
    }
}
