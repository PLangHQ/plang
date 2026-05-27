using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace app.channels.serializers.serializer.plang;

/// <summary>
/// The canonical PLang-to-PLang transport serializer (<c>application/plang</c>).
///
/// <para>
/// Composes its STJ options from a fresh base (camelCase + null-skip), then adds
/// the path converter (Context-bound when available), the
/// <see cref="global::app.data.WireJsonConverter"/> (sign-if-missing during the
/// walk + canonical four-field shape), and
/// <see cref="global::app.channels.serializers.filters.Transport.ForOutbound"/>
/// (re-includes [Out] properties like Signature).
/// </para>
///
/// <para>
/// Notably the merged options do NOT chain
/// <see cref="global::app.channels.serializers.filters.Sensitive.Strip"/> in —
/// PLang's own settings/sqlite store rides on this serializer to persist
/// Identity (whose PrivateKey is marked [Sensitive]). Sensitive-stripping is
/// the responsibility of channels that publish externally (HTTP responses,
/// external JSON), and lives on the base <see cref="global::app.channels.serializers.serializer.Json"/>
/// rather than the inter-actor transport serializer.
/// </para>
///
/// <para>
/// Read does NOT auto-verify — verification is the consumer's explicit step
/// (<c>signing.verify</c> action, or a channel event handler bound to
/// <c>BeforeRead</c>/<c>AfterRead</c>). The reconstructed Data has its signature
/// populated-but-unverified.
/// </para>
/// </summary>
public sealed class @this : ISerializer
{
    public string Type => "application/plang";
    public string Extension => ".plang";

    private readonly JsonSerializerOptions _outbound;
    private readonly JsonSerializerOptions _inbound;

    public @this() : this(null) { }

    public @this(actor.context.@this? context)
    {
        var pathConverter = context != null
            ? new global::app.types.path.JsonConverter(context)
            : new global::app.types.path.JsonConverter();

        _outbound = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
                new global::app.data.WireJsonConverter(),
                pathConverter,
            },
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { global::app.channels.serializers.filters.Transport.ForOutbound }
            }
        };

        _inbound = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
                new global::app.data.WireJsonConverter(),
                pathConverter,
            },
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { global::app.channels.serializers.filters.Transport.ForInbound }
            }
        };
    }

    /// <summary>
    /// Raw outbound options — exposed for canonicalization (crypto.Hash) so the
    /// signed bytes match the wire bytes.
    /// </summary>
    internal JsonSerializerOptions OutboundOptions => _outbound;

    public async Task<global::app.data.@this> SerializeAsync(Stream stream, global::app.data.@this data, CancellationToken cancellationToken = default)
    {
        try
        {
            await JsonSerializer.SerializeAsync(stream, data, _outbound, cancellationToken);
            return global::app.data.@this.Ok();
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException)
        {
            return global::app.data.@this.FromError(new errors.ServiceError(
                $"Plang serialize failed: {ex.Message}", "PlangSerializeError", 400) { Exception = ex });
        }
    }

    public async Task<global::app.data.@this> DeserializeAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        try
        {
            if (stream.Length == 0) return global::app.data.@this.Ok();
            var v = await JsonSerializer.DeserializeAsync<global::app.data.@this>(stream, _inbound, cancellationToken);
            return global::app.data.@this.Ok(v);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException)
        {
            return global::app.data.@this.FromError(new errors.ServiceError(
                $"Plang deserialize failed: {ex.Message}", "PlangDeserializeError", 400) { Exception = ex });
        }
    }

    public async Task<global::app.data.@this<T>> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
    {
        try
        {
            if (stream.Length == 0) return global::app.data.@this<T>.Ok(default!);
            var v = await JsonSerializer.DeserializeAsync<T>(stream, _inbound, cancellationToken);
            return global::app.data.@this<T>.Ok(v!);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException)
        {
            return global::app.data.@this<T>.FromError(new errors.ServiceError(
                $"Plang deserialize failed: {ex.Message}", "PlangDeserializeError", 400) { Exception = ex });
        }
    }

    public global::app.data.@this<string> Serialize(global::app.data.@this data)
    {
        try
        {
            return global::app.data.@this<string>.Ok(JsonSerializer.Serialize(data, _outbound));
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return global::app.data.@this<string>.FromError(new errors.ServiceError(
                $"Plang serialize failed: {ex.Message}", "PlangSerializeError", 400) { Exception = ex });
        }
    }

    public global::app.data.@this Deserialize(string s)
    {
        try
        {
            if (string.IsNullOrEmpty(s) || s == "null") return global::app.data.@this.Ok();
            return global::app.data.@this.Ok(JsonSerializer.Deserialize<global::app.data.@this>(s, _inbound));
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return global::app.data.@this.FromError(new errors.ServiceError(
                $"Plang deserialize failed: {ex.Message}", "PlangDeserializeError", 400) { Exception = ex });
        }
    }

    public global::app.data.@this<T> Deserialize<T>(string s)
    {
        try
        {
            if (string.IsNullOrEmpty(s) || s == "null") return global::app.data.@this<T>.Ok(default!);
            return global::app.data.@this<T>.Ok(JsonSerializer.Deserialize<T>(s, _inbound)!);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return global::app.data.@this<T>.FromError(new errors.ServiceError(
                $"Plang deserialize failed: {ex.Message}", "PlangDeserializeError", 400) { Exception = ex });
        }
    }
}
