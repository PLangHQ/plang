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
            // The value writes ITSELF straight to the stream (one lazy pass, no pre-resolve
            // walk). A leaf renders bare; a container renders via its serializer/text.cs
            // override (json string). The writer owns the stream and the rendering.
            var writer = new global::app.channel.serializer.text.Writer(stream, _encoding);
            await data.Output(writer, global::app.View.Out, data.Context);
            await stream.FlushAsync(cancellationToken);
            return global::app.data.@this.Ok();
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException)
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

    public async Task<data.@this<T>> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default) where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
    {
        var result = await DeserializeAsync(stream, cancellationToken);
        if (!result.Success) return global::app.data.@this<T>.From(result);
        // Empty payload = absence — the channel-protocol concern that belongs
        // to this serializer. Everything else: the Data converts ITSELF to T
        // through T's own Convert hook (As<T> is the typed resolution door).
        if (await result.IsEmpty()) return global::app.data.@this<T>.Ok(default!);
        return result.ShallowClone<T>(await result.Value<T>());
    }

    public data.@this<global::app.type.text.@this> Serialize(data.@this data)
    {
        var value = data.Peek();
        if (value == null || value.IsNull)
            return global::app.data.@this<global::app.type.text.@this>.Ok("");
        if (value.IsLeaf)
            return global::app.data.@this<global::app.type.text.@this>.Ok(value.ToString() ?? "");
        return _jsonFallback.Serialize(data);
    }

    public data.@this Deserialize(string s)
        => global::app.data.@this.Ok(s);

    public data.@this<T> Deserialize<T>(string s) where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
        => global::app.data.@this<T>.Ok(FromText<T>(s));

    /// <summary>
    /// A text payload → a typed value. The serializer only supplies the text; the
    /// TARGET type creates itself from it (<c>number.Create</c> parses "5"→number via
    /// its own hook) — the type owns its from-text construction, not this serializer.
    /// An empty payload is absence.
    /// </summary>
    private T? FromText<T>(string s) where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
    {
        if (string.IsNullOrEmpty(s)) return null;
        var text = (global::app.type.text.@this)s;
        return T.Create(text, new global::app.data.@this("", text));
    }
}
