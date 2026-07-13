using System.Text;

namespace app.channel.serializer;

/// <summary>
/// Plain-text serializer — the value writes ITSELF via <c>data.Output</c> into a
/// <see cref="text.Writer"/>: a leaf renders bare, a container renders as json (the writer owns
/// <c>BeginObject</c>/<c>BeginArray</c>, no per-type override). No Data envelope.
/// </summary>
public sealed class Text : ISerializer
{
    /// <summary>The value/content mimetype this serializer owns — a bare value (text, a
    /// path, a biginteger's digits) read through the value reader, written as a quoted
    /// string. Every other (json-encoding) format rides inline.</summary>
    public const string Mime = "text/plain";

    public string Type => Mime;
    public string Extension => ".txt";

    private readonly Encoding _encoding;
    // The context deserialized values are born on — Deserialize has no Data to source one from
    // (unlike Serialize, which uses data.Context). Born-with-context: a serializer belongs to an
    // actor and an actor always has a context, so this is non-null.
    private readonly actor.context.@this _context;

    public Text(actor.context.@this context, Encoding? encoding = null)
    {
        _context = context;
        _encoding = encoding ?? Encoding.UTF8;
    }

    public async Task<data.@this> SerializeAsync(Stream stream, data.@this data, global::app.View view = global::app.View.Out, CancellationToken cancellationToken = default)
    {
        try
        {
            // The value writes ITSELF straight to the stream (one lazy pass, no pre-resolve
            // walk). A leaf renders bare; a container renders via its format text serializer
            // (json string). The writer owns the stream and the rendering.
            var writer = new global::app.channel.serializer.text.Writer(stream, _encoding);
            await data.Output(writer, view, data.Context);
            await stream.FlushAsync(cancellationToken);
            return data.Context.Ok();
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException)
        {
            return data.Context.Error(new error.ServiceError(
                $"Text serialize failed: {ex.Message}", "TextSerializeError", 400) { Exception = ex });
        }
    }

    public async Task<data.@this> DeserializeAsync(Stream stream, global::app.View view = global::app.View.Out, CancellationToken cancellationToken = default)
    {
        try
        {
            using var reader = new StreamReader(stream, _encoding, leaveOpen: true);
            var text = await reader.ReadToEndAsync(cancellationToken);
            return _context.Ok(text);
        }
        catch (Exception ex) when (ex is IOException)
        {
            return _context.Error(new error.ServiceError(
                $"Text deserialize failed: {ex.Message}", "TextDeserializeError", 400) { Exception = ex });
        }
    }

    public async Task<data.@this<T>> DeserializeAsync<T>(Stream stream, global::app.View view = global::app.View.Out, CancellationToken cancellationToken = default) where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
    {
        var result = await DeserializeAsync(stream, view, cancellationToken);
        if (!result.Success) return global::app.data.@this<T>.From(result);
        // Empty payload = absence — the channel-protocol concern that belongs
        // to this serializer. Everything else: the Data converts ITSELF to T
        // through T's own Convert hook (As<T> is the typed resolution door).
        if (await result.IsEmpty()) return result.Context.Ok<T>(default!);
        return result.ShallowClone<T>(await result.Value<T>());
    }
}
