namespace app.module.code.dll.loaded.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for a loaded DLL's record — the read-side mirror of its
/// tagged write: its <c>source</c>, read as a path is. A record without one is declined, saying so.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        var types = ctx.Context.App.type.list;
        if (reader.Null()) return new global::app.type.item.@null.@this(types[new global::app.type.@this(typeof(loaded.@this), kind), ctx.Context]);

        global::app.type.item.path.@this? source = null;
        reader.BeginObject();
        while (reader.NextName(out var name))
        {
            if (name == "source") source = new global::app.type.item.path.serializer.Reader().Read(ref reader, null, ctx) as global::app.type.item.path.@this;
            else reader.Skip();
        }
        reader.EndObject();

        return new loaded.@this(types[typeof(loaded.@this)])
        {
            Source = source ?? throw new global::app.error.DeclinedException(new global::app.error.@this(
                "a loaded DLL's record holds no source to load it from", "DllRecordIncomplete", 400)),
        };
    }
}
