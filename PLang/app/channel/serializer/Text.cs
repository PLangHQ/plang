using System.Text;

namespace app.channel.serializer;

/// <summary>
/// Plain text serializer - emits data.Value as its string representation.
/// Falls back to JSON for complex types so that e.g. List&lt;T&gt; outputs proper JSON
/// instead of "System.Collections.Generic.List`1[...]".
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
    private readonly global::app.channel.serializer.Json _jsonFallback;
    // The context deserialized values are born on — Deserialize has no Data to source one from
    // (unlike Serialize, which uses data.Context). Born-with-context: a serializer belongs to an
    // actor and an actor always has a context, so this is non-null.
    private readonly actor.context.@this _context;

    public Text(actor.context.@this context, Encoding? encoding = null, global::app.channel.serializer.Json? jsonFallback = null)
    {
        _context = context;
        _encoding = encoding ?? Encoding.UTF8;
        _jsonFallback = jsonFallback ?? new global::app.channel.serializer.Json(context);
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

    /// <summary>
    /// Reads a held value into its plang type — a text value IS the raw string, so it
    /// makes a <see cref="global::app.channel.serializer.value.Reader"/> over it
    /// (one scalar token) and lets the type pull itself off it.
    /// </summary>
    public global::app.type.item.@this Read(global::app.type.item.source source, global::app.type.reader.ReadContext ctx)
    {
        var type = source.Mint();
        var typeReader = ctx.Context.App.Type.Readers.Reader(type.Name, type.Kind, ctx.Context);
        var reader = new global::app.channel.serializer.value.Reader(source.Raw);
        return typeReader.Read(ref reader, type.Kind, ctx);
    }
}
