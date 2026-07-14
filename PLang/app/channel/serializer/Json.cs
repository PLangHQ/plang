using System.Text.Json;

namespace app.channel.serializer;

/// <summary>
/// JSON serializer — the bare value view (no <c>{name,type,…}</c> envelope).
/// Write drives the <see cref="IWriter"/> path so the value writes ITSELF (its own
/// <c>Output → Write</c>); read decodes one json payload to its native plang item
/// via the json kind's own parse (<c>Kind["json"].Parse</c> — structured → clr(json),
/// scalar → its native leaf). No STJ serializer layer; only the Utf8 tokenizer.
/// </summary>
public sealed class Json : ISerializer
{
    public string Type => "application/json";
    public string Extension => ".json";

    private readonly actor.context.@this _context;

    // Born-with-context: a serializer belongs to an actor, and an actor always has a context —
    // it's the context deserialized values are born on. There is no context-less serializer.
    public Json(actor.context.@this context) => _context = context;

    public async Task<data.@this> SerializeAsync(Stream stream, data.@this data, global::app.View view = global::app.View.Out, CancellationToken cancellationToken = default)
    {
        try
        {
            // JSON is the "value as JSON view" — the channel drives the writer and the value
            // writes ITSELF (its own Output → Write(IWriter)); no STJ in the value path, no
            // per-type converters. Bare: emitsSchema:false skips the {name,type,…} envelope,
            // so the value rides alone (type inferred on read).
            await using var utf8 = new Utf8JsonWriter(stream);
            var writer = new global::app.channel.serializer.json.Writer(
                utf8, view, _context.App.Type.Renderer, emitsSchema: false);
            await data.Output(writer, view, _context);
            await utf8.FlushAsync(cancellationToken);
            return data.Context.Ok();
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException)
        {
            return data.Context.Error(new error.ServiceError(
                $"JSON serialize failed: {ex.Message}", "JsonSerializeError", 400) { Exception = ex });
        }
    }

    public async Task<data.@this> DeserializeAsync(Stream stream, global::app.View view = global::app.View.Out, CancellationToken cancellationToken = default)
    {
        try
        {
            if (stream.CanSeek && stream.Length == 0) return _context.Ok();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken);
            if (ms.Length == 0) return _context.Ok();
            // A bare json payload — the json kind owns its parse (structured → clr(json),
            // scalar → its native leaf). One decode, no STJ serializer layer.
            var item = _context.App.Type.Kind["json"].Parse(ms.ToArray(), _context);
            return _context.Ok(item);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException)
        {
            return _context.Error(new error.ServiceError(
                $"JSON deserialize failed: {ex.Message}", "JsonDeserializeError", 400) { Exception = ex });
        }
    }

    public async Task<data.@this<T>> DeserializeAsync<T>(Stream stream, global::app.View view = global::app.View.Out, CancellationToken cancellationToken = default) where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
    {
        // Typed read — born at the wire type through the same one path, handed back as a
        // typed FACE via As<T> (no resolution, no value copy), mirroring the plang transport.
        var data = await DeserializeAsync(stream, view, cancellationToken);
        if (!data.Success) return global::app.data.@this<T>.From(data);
        return data.As<T>();
    }
}
