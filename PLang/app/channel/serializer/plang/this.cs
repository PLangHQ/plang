namespace app.channel.serializer.plang;

/// <summary>
/// The serializer list's entry for <c>application/plang</c>: it forwards to the formats that own the work —
/// the wire type's <c>plang</c> kind for a whole Data (sign, layer, verify), goal's format for a bare item
/// (a <c>.pr</c>). It goes with the serializer list.
/// </summary>
public sealed class @this : ISerializer
{
    public string Type => "application/plang";
    public string Extension => ".plang";

    private readonly actor.context.@this _context;

    public @this(actor.context.@this context) => _context = context;

    private global::app.type.item.wire.kind.plang.@this Format
        => (global::app.type.item.wire.kind.plang.@this)_context.App.type.list["wire"].kind["plang"]!;

    private global::app.type.kind.@this Program => _context.App.type.list["goal"].kind;

    public Task<global::app.data.@this> SerializeAsync(Stream stream, global::app.data.@this data, global::app.View view = global::app.View.Out, CancellationToken cancellationToken = default)
        => Format.Encode(stream, data, _context, view, ct: cancellationToken);

    public async Task SerializeItemAsync(Stream stream, global::app.type.item.@this item,
        global::app.View view = global::app.View.Store, CancellationToken cancellationToken = default)
        => await Program.Encode(stream, _context.Ok(item), _context, view, ct: cancellationToken);

    public async Task<string> Text(global::app.type.item.@this item, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        await Program.Encode(ms, _context.Ok(item), _context, global::app.View.Store, ct: cancellationToken);
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    public async Task SerializeItemsAsync(Stream stream, IEnumerable<global::app.type.item.@this> items,
        global::app.View view = global::app.View.Store, CancellationToken cancellationToken = default)
        => await Program.Encode(stream, _context.Ok(new global::app.type.item.list.@this(items)), _context, view, ct: cancellationToken);

    public async Task<global::app.data.@this> DeserializeAsync(Stream stream, global::app.View view = global::app.View.Out, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        return await Format.Decode(ms.ToArray(), _context, view: view, ct: cancellationToken);
    }

    public async Task<global::app.data.@this<T>> DeserializeAsync<T>(Stream stream, global::app.View view = global::app.View.Out, CancellationToken cancellationToken = default) where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
    {
        var data = await DeserializeAsync(stream, view, cancellationToken);
        if (!data.Success) return global::app.data.@this<T>.From(data);
        return data.As<T>();
    }

    public global::app.type.item.@this Read(global::app.type.item.source source, global::app.type.reader.ReadContext ctx)
        => Format.Read(source, ctx);

    public bool Owns(global::app.channel.serializer.IWriter writer) => Format.Owns(writer);
}
