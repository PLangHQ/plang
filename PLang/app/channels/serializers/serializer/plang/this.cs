using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace app.channels.serializers.serializer.plang;

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

    public @this() : this(null) { }

    /// <summary>
    /// Construct with an actor Context — the bundled PathJsonConverter will
    /// route through <c>path.Resolve(raw, context)</c> on read, so deserialized
    /// Goal/GoalCall/... carry Path-typed fields fully Context-wired. Per-Actor
    /// instances pass their Context here.
    /// </summary>
    public @this(actor.context.@this? context)
    {
        var pathConverter = context != null
            ? new global::app.types.path.JsonConverter(context)
            : new global::app.types.path.JsonConverter();
        _serializeOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase), new global::app.data.Json(), pathConverter },
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { global::app.channels.serializers.filters.Transport.ForOutbound }
            }
        };

        _deserializeOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase), new global::app.data.Json(), pathConverter },
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { global::app.channels.serializers.filters.Transport.ForInbound }
            }
        };
    }

    public async Task<data.@this> SerializeAsync(Stream stream, object? value, Type? type = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (value == null)
            {
                await stream.WriteAsync("null"u8.ToArray(), cancellationToken);
                return data.@this.Ok();
            }
            await JsonSerializer.SerializeAsync(stream, value, type ?? value.GetType(), _serializeOptions, cancellationToken);
            return data.@this.Ok();
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException)
        {
            return data.@this.FromError(new errors.ServiceError(
                $"Plang serialize failed: {ex.Message}", "PlangSerializeError", 400) { Exception = ex });
        }
    }

    public async Task<data.@this> DeserializeAsync(Stream stream, Type type, CancellationToken cancellationToken = default)
    {
        try
        {
            if (stream.Length == 0) return data.@this.Ok();
            var v = await JsonSerializer.DeserializeAsync(stream, type, _deserializeOptions, cancellationToken);
            return data.@this.Ok(v);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException)
        {
            return data.@this.FromError(new errors.ServiceError(
                $"Plang deserialize failed: {ex.Message}", "PlangDeserializeError", 400) { Exception = ex });
        }
    }

    public async Task<data.@this<T>> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
    {
        try
        {
            if (stream.Length == 0) return data.@this<T>.Ok(default!);
            var v = await JsonSerializer.DeserializeAsync<T>(stream, _deserializeOptions, cancellationToken);
            return data.@this<T>.Ok(v!);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException)
        {
            return data.@this<T>.FromError(new errors.ServiceError(
                $"Plang deserialize failed: {ex.Message}", "PlangDeserializeError", 400) { Exception = ex });
        }
    }

    public data.@this<string> Serialize(object? value, Type? type = null)
    {
        try
        {
            if (value == null) return data.@this<string>.Ok("null");
            return data.@this<string>.Ok(JsonSerializer.Serialize(value, type ?? value.GetType(), _serializeOptions));
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return data.@this<string>.FromError(new errors.ServiceError(
                $"Plang serialize failed: {ex.Message}", "PlangSerializeError", 400) { Exception = ex });
        }
    }

    public data.@this Deserialize(string data, Type type)
    {
        try
        {
            if (string.IsNullOrEmpty(data) || data == "null") return global::app.data.@this.Ok();
            return global::app.data.@this.Ok(JsonSerializer.Deserialize(data, type, _deserializeOptions));
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return global::app.data.@this.FromError(new errors.ServiceError(
                $"Plang deserialize failed: {ex.Message}", "PlangDeserializeError", 400) { Exception = ex });
        }
    }

    public data.@this<T> Deserialize<T>(string data)
    {
        try
        {
            if (string.IsNullOrEmpty(data) || data == "null") return global::app.data.@this<T>.Ok(default!);
            return global::app.data.@this<T>.Ok(JsonSerializer.Deserialize<T>(data, _deserializeOptions)!);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return global::app.data.@this<T>.FromError(new errors.ServiceError(
                $"Plang deserialize failed: {ex.Message}", "PlangDeserializeError", 400) { Exception = ex });
        }
    }
}
