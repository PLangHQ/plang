using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace app.channels.serializers.serializer.plang;

/// <summary>
/// Wire serializer for the <c>application/plang+data</c> mimetype — emits the *full* Data
/// envelope: <c>Type</c> + <c>Value</c> + <c>Signature</c>. Unlike <see cref="global::app.channels.serializers.serializer.plang.@this"/>
/// (which targets <c>application/plang</c> for the older PLang-to-PLang transport) this one
/// is the wire shape callbacks ride on. Reading <c>data.Signature</c> on Write triggers lazy
/// signing via <see cref="global::app.data.@this.EnsureSigned"/> when not already populated.
///
/// Read does NOT auto-verify — verification is the consumer's explicit step (callback.run
/// invokes <c>signing.verify</c> before dispatching). The reconstructed Data has its
/// signature populated-but-unverified.
/// </summary>
public sealed class Data : ISerializer
{
    public string ContentType => "application/plang+data";
    public string FileExtension => ".pdata";

    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        // Strip [Sensitive]-marked properties from the envelope's Value object —
        // mirrors global::app.data.@this._envelopeJsonOptions.
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { global::app.channels.serializers.filters.Sensitive.Strip }
        }
    };

    public async Task<global::app.data.@this> SerializeAsync(Stream stream, object? value, Type? type = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (value is global::app.data.@this d)
            {
                d.EnsureSigned();
                var envelope = new Envelope
                {
                    Type = d.Type?.Value ?? "",
                    Value = d.Value,
                    Signature = d.RawSignature
                };
                await JsonSerializer.SerializeAsync(stream, envelope, _options, cancellationToken);
                return global::app.data.@this.Ok();
            }
            if (value == null)
            {
                await stream.WriteAsync("null"u8.ToArray(), cancellationToken);
                return global::app.data.@this.Ok();
            }
            await JsonSerializer.SerializeAsync(stream, value, type ?? value.GetType(), _options, cancellationToken);
            return global::app.data.@this.Ok();
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException)
        {
            return global::app.data.@this.FromError(new global::app.errors.ServiceError(
                $"plang+data serialize failed: {ex.Message}", "PlangDataSerializeError", 400) { Exception = ex });
        }
    }

    public async Task<global::app.data.@this> DeserializeAsync(Stream stream, Type type, CancellationToken cancellationToken = default)
    {
        try
        {
            if (stream.Length == 0) return global::app.data.@this.Ok();
            if (type == typeof(global::app.data.@this))
            {
                var env = await JsonSerializer.DeserializeAsync<Envelope>(stream, _options, cancellationToken);
                return global::app.data.@this.Ok(env != null ? FromEnvelope(env) : null);
            }
            var v = await JsonSerializer.DeserializeAsync(stream, type, _options, cancellationToken);
            return global::app.data.@this.Ok(v);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException)
        {
            return global::app.data.@this.FromError(new global::app.errors.ServiceError(
                $"plang+data deserialize failed: {ex.Message}", "PlangDataDeserializeError", 400) { Exception = ex });
        }
    }

    public async Task<global::app.data.@this<T>> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
    {
        try
        {
            if (stream.Length == 0) return global::app.data.@this<T>.Ok(default!);
            if (typeof(T) == typeof(global::app.data.@this))
            {
                var env = await JsonSerializer.DeserializeAsync<Envelope>(stream, _options, cancellationToken);
                return global::app.data.@this<T>.Ok(env != null ? (T)(object)FromEnvelope(env) : default!);
            }
            var v = await JsonSerializer.DeserializeAsync<T>(stream, _options, cancellationToken);
            return global::app.data.@this<T>.Ok(v!);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException)
        {
            return global::app.data.@this<T>.FromError(new global::app.errors.ServiceError(
                $"plang+data deserialize failed: {ex.Message}", "PlangDataDeserializeError", 400) { Exception = ex });
        }
    }

    public global::app.data.@this<string> Serialize(object? value, Type? type = null)
    {
        try
        {
            if (value is global::app.data.@this d)
            {
                d.EnsureSigned();
                var envelope = new Envelope
                {
                    Type = d.Type?.Value ?? "",
                    Value = d.Value,
                    Signature = d.RawSignature
                };
                return global::app.data.@this<string>.Ok(JsonSerializer.Serialize(envelope, _options));
            }
            if (value == null) return global::app.data.@this<string>.Ok("null");
            return global::app.data.@this<string>.Ok(JsonSerializer.Serialize(value, type ?? value.GetType(), _options));
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return global::app.data.@this<string>.FromError(new global::app.errors.ServiceError(
                $"plang+data serialize failed: {ex.Message}", "PlangDataSerializeError", 400) { Exception = ex });
        }
    }

    public global::app.data.@this Deserialize(string data, Type type)
    {
        try
        {
            if (string.IsNullOrEmpty(data) || data == "null") return global::app.data.@this.Ok();
            if (type == typeof(global::app.data.@this))
            {
                var env = JsonSerializer.Deserialize<Envelope>(data, _options);
                return global::app.data.@this.Ok(env != null ? FromEnvelope(env) : null);
            }
            return global::app.data.@this.Ok(JsonSerializer.Deserialize(data, type, _options));
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return global::app.data.@this.FromError(new global::app.errors.ServiceError(
                $"plang+data deserialize failed: {ex.Message}", "PlangDataDeserializeError", 400) { Exception = ex });
        }
    }

    public global::app.data.@this<T> Deserialize<T>(string data)
    {
        try
        {
            if (string.IsNullOrEmpty(data) || data == "null") return global::app.data.@this<T>.Ok(default!);
            if (typeof(T) == typeof(global::app.data.@this))
            {
                var env = JsonSerializer.Deserialize<Envelope>(data, _options);
                return global::app.data.@this<T>.Ok(env != null ? (T)(object)FromEnvelope(env) : default!);
            }
            return global::app.data.@this<T>.Ok(JsonSerializer.Deserialize<T>(data, _options)!);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return global::app.data.@this<T>.FromError(new global::app.errors.ServiceError(
                $"plang+data deserialize failed: {ex.Message}", "PlangDataDeserializeError", 400) { Exception = ex });
        }
    }

    private static global::app.data.@this FromEnvelope(Envelope env)
    {
        var d = new global::app.data.@this("", env.Value,
            string.IsNullOrEmpty(env.Type) ? null : global::app.data.type.FromName(env.Type));
        d.Signature = env.Signature;
        return d;
    }

    /// <summary>Wire shape — pinned property order: type, value, signature.</summary>
    internal sealed class Envelope
    {
        [JsonPropertyOrder(1)] public string Type { get; set; } = "";
        [JsonPropertyOrder(2)] public object? Value { get; set; }
        // Wire envelope DTO; setter required for STJ deserialization. The trust
        // gate is VerifyAsync run on the populated Data, not the setter on this
        // transport shape.
        [JsonPropertyOrder(3)] public app.modules.signing.Signature? Signature { get; set; } // nosemgrep: plang-verified-must-have-private-setter
    }
}
