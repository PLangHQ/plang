using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace App.Channels.Serializers.Serializer;

/// <summary>
/// Wire serializer for the <c>application/plang+data</c> mimetype — emits the *full* Data
/// envelope: <c>Type</c> + <c>Value</c> + <c>Signature</c>. Unlike <see cref="PlangSerializer"/>
/// (which targets <c>application/plang</c> for the older PLang-to-PLang transport) this one
/// is the wire shape callbacks ride on. Reading <c>data.Signature</c> on Write triggers lazy
/// signing via <see cref="Data.@this.EnsureSigned"/> when not already populated.
///
/// Read does NOT auto-verify — verification is the consumer's explicit step (callback.run
/// invokes <c>signing.verify</c> before dispatching). The reconstructed Data has its
/// signature populated-but-unverified.
/// </summary>
public sealed class PlangDataSerializer : ISerializer
{
    public string ContentType => "application/plang+data";
    public string FileExtension => ".pdata";

    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        // Strip [Sensitive]-marked properties from the envelope's Value object —
        // mirrors Data.@this._envelopeJsonOptions. Security v1 S-F4.
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { SensitivePropertyFilter.Strip }
        }
    };

    public async Task SerializeAsync(Stream stream, object? value, Type? type = null,
        CancellationToken cancellationToken = default)
    {
        if (value is Data.@this data)
        {
            data.EnsureSigned();
            var envelope = new Envelope
            {
                Type = data.Type?.Value ?? "",
                Value = data.Value,
                Signature = data.RawSignature
            };
            await JsonSerializer.SerializeAsync(stream, envelope, _options, cancellationToken);
            return;
        }
        if (value == null)
        {
            await stream.WriteAsync("null"u8.ToArray(), cancellationToken);
            return;
        }
        await JsonSerializer.SerializeAsync(stream, value, type ?? value.GetType(), _options, cancellationToken);
    }

    public async Task<object?> DeserializeAsync(Stream stream, Type type, CancellationToken cancellationToken = default)
    {
        if (stream.Length == 0) return null;
        if (type == typeof(Data.@this))
        {
            var env = await JsonSerializer.DeserializeAsync<Envelope>(stream, _options, cancellationToken);
            return env != null ? FromEnvelope(env) : null;
        }
        return await JsonSerializer.DeserializeAsync(stream, type, _options, cancellationToken);
    }

    public async Task<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
    {
        if (stream.Length == 0) return default;
        if (typeof(T) == typeof(Data.@this))
        {
            var env = await JsonSerializer.DeserializeAsync<Envelope>(stream, _options, cancellationToken);
            return env != null ? (T)(object)FromEnvelope(env) : default;
        }
        return await JsonSerializer.DeserializeAsync<T>(stream, _options, cancellationToken);
    }

    public string Serialize(object? value, Type? type = null)
    {
        if (value is Data.@this data)
        {
            data.EnsureSigned();
            var envelope = new Envelope
            {
                Type = data.Type?.Value ?? "",
                Value = data.Value,
                Signature = data.RawSignature
            };
            return JsonSerializer.Serialize(envelope, _options);
        }
        if (value == null) return "null";
        return JsonSerializer.Serialize(value, type ?? value.GetType(), _options);
    }

    public object? Deserialize(string data, Type type)
    {
        if (string.IsNullOrEmpty(data) || data == "null") return null;
        if (type == typeof(Data.@this))
        {
            var env = JsonSerializer.Deserialize<Envelope>(data, _options);
            return env != null ? FromEnvelope(env) : null;
        }
        return JsonSerializer.Deserialize(data, type, _options);
    }

    public T? Deserialize<T>(string data)
    {
        if (string.IsNullOrEmpty(data) || data == "null") return default;
        if (typeof(T) == typeof(Data.@this))
        {
            var env = JsonSerializer.Deserialize<Envelope>(data, _options);
            return env != null ? (T)(object)FromEnvelope(env) : default;
        }
        return JsonSerializer.Deserialize<T>(data, _options);
    }

    private static Data.@this FromEnvelope(Envelope env)
    {
        var d = new Data.@this("", env.Value,
            string.IsNullOrEmpty(env.Type) ? null : Data.Type.FromName(env.Type));
        d.Signature = env.Signature;
        return d;
    }

    /// <summary>Wire shape — pinned property order: type, value, signature.</summary>
    internal sealed class Envelope
    {
        [JsonPropertyOrder(1)] public string Type { get; set; } = "";
        [JsonPropertyOrder(2)] public object? Value { get; set; }
        [JsonPropertyOrder(3)] public App.modules.signing.Signature? Signature { get; set; }
    }
}
