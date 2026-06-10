using System.Text;

namespace app.channel.serializer;

/// <summary>
/// Plain text serializer - emits data.Value as its string representation.
/// Falls back to JSON for complex types so that e.g. List&lt;T&gt; outputs proper JSON
/// instead of "System.Collections.Generic.List`1[...]".
/// </summary>
public sealed class Text : ISerializer
{
    public string Type => "text/plain";
    public string Extension => ".txt";

    private readonly Encoding _encoding;
    private readonly global::app.channel.serializer.Json _jsonFallback;

    public Text(Encoding? encoding = null, global::app.channel.serializer.Json? jsonFallback = null)
    {
        _encoding = encoding ?? Encoding.UTF8;
        _jsonFallback = jsonFallback ?? new global::app.channel.serializer.Json();
    }

    public async Task<data.@this> SerializeAsync(Stream stream, data.@this data, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await data.Value();
            // A scalar wrapper (text/number/bool/…) is a text leaf — render it bare
            // via ToString (born-native: it's no longer a CLR primitive but IS a leaf).
            // Only genuine containers/domain objects fall back to JSON.
            if (value != null && !AppTypes.IsPrimitive(value.GetType())
                && value is not global::app.type.item.@this { IsLeaf: true })
                return await _jsonFallback.SerializeAsync(stream, data, cancellationToken);

            var bytes = _encoding.GetBytes((value?.ToString() ?? "") + Environment.NewLine);
            await stream.WriteAsync(bytes, cancellationToken);
            return global::app.data.@this.Ok();
        }
        catch (Exception ex) when (ex is IOException)
        {
            return global::app.data.@this.FromError(new error.ServiceError(
                $"Text serialize failed: {ex.Message}", "TextSerializeError", 400) { Exception = ex });
        }
    }

    public async Task<data.@this> DeserializeAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        try
        {
            using var reader = new StreamReader(stream, _encoding, leaveOpen: true);
            var text = await reader.ReadToEndAsync(cancellationToken);
            return global::app.data.@this.Ok(text);
        }
        catch (Exception ex) when (ex is IOException)
        {
            return global::app.data.@this.FromError(new error.ServiceError(
                $"Text deserialize failed: {ex.Message}", "TextDeserializeError", 400) { Exception = ex });
        }
    }

    public async Task<data.@this<T>> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default) where T : global::app.type.item.@this
    {
        var result = await DeserializeAsync(stream, cancellationToken);
        if (!result.Success) return global::app.data.@this<T>.From(result);
        // The raw stream text rides born-native as text — its string face
        // feeds the typed parse.
        return global::app.data.@this<T>.Ok(FromText<T>(result.Materialize()?.ToString() ?? ""));
    }

    public data.@this<global::app.type.text.@this> Serialize(data.@this data)
    {
        var value = data.Materialize();
        if (value == null || AppTypes.IsPrimitive(value.GetType())
            || value is global::app.type.item.@this { IsLeaf: true })
            return global::app.data.@this<global::app.type.text.@this>.Ok(value?.ToString() ?? "");
        return _jsonFallback.Serialize(data);
    }

    public data.@this Deserialize(string s)
        => global::app.data.@this.Ok(s);

    public data.@this<T> Deserialize<T>(string s) where T : global::app.type.item.@this
        => global::app.data.@this<T>.Ok(FromText<T>(s));

    /// <summary>
    /// A text payload → a typed value. Two concerns belong to this serializer, not
    /// the general converter: an empty payload is absence (<c>default</c>, not a
    /// value), and a text payload's own bytes are its UTF-8 encoding. Everything
    /// else routes through the one converter (invariant culture, residual primitive
    /// leaf + per-type hooks) — the text channel no longer forks its own parse, which
    /// had drifted to CurrentCulture and gave a divergent locale result.
    /// </summary>
    private T FromText<T>(string s)
    {
        if (string.IsNullOrEmpty(s)) return default!;
        if (typeof(T) == typeof(byte[])) return (T)(object)_encoding.GetBytes(s);
        var converted = AppTypes.ConvertTo(s, typeof(T));
        return converted is T typed ? typed : default!;
    }
}
