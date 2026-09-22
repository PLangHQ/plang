namespace app.module.action.code.registration.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for <c>registration</c> — the
/// read-side mirror of the tagged write. Lazy like any content value: Restore reaches a row through
/// the typed ask, and the slice materializes here.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("registration", kind);

        string typeName = "", providerName = "";
        string? source = null;

        reader.BeginObject();
        while (reader.NextName(out var name))
        {
            switch (name)
            {
                case "typeName": typeName = reader.String(); break;
                case "providerName": providerName = reader.String(); break;
                case "source": source = reader.Null() ? null : reader.String(); break;
                default: reader.Skip(); break;
            }
        }
        reader.EndObject();

        return new global::app.module.action.code.registration.@this
        {
            TypeName = typeName,
            ProviderName = providerName,
            Source = source,
        };
    }
}
