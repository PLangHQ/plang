namespace app.module.code.dll.added.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for an added DLL's record — the read-side mirror of its
/// tagged write: its <c>source</c>, read as a path is, and its <c>namespace</c>. A record without a source is declined,
/// saying so.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        var types = ctx.Context.App.type.list;
        if (reader.Null()) return new global::app.type.item.@null.@this(types[new global::app.type.@this(typeof(added.@this), kind), ctx.Context]);

        global::app.type.item.path.@this? source = null;
        string? @namespace = null;
        reader.BeginObject();
        while (reader.NextName(out var name))
        {
            switch (name)
            {
                case "source": source = new global::app.type.item.path.serializer.Reader().Read(ref reader, null, ctx) as global::app.type.item.path.@this; break;
                case "namespace": @namespace = reader.Null() ? null : reader.String(); break;
                default: reader.Skip(); break;
            }
        }
        reader.EndObject();

        return new added.@this(types[typeof(added.@this)])
        {
            Source = source ?? throw new global::app.error.DeclinedException(new global::app.error.@this(
                "an added DLL's record holds no source to add it from", "DllRecordIncomplete", 400)),
            Namespace = @namespace,
        };
    }
}
