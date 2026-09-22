namespace app.module.action.code.defaultoverride.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for <c>defaultoverride</c> — the
/// read-side mirror of the tagged write.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("defaultoverride", kind);

        string typeName = "", providerName = "";

        reader.BeginObject();
        while (reader.NextName(out var name))
        {
            switch (name)
            {
                case "typeName": typeName = reader.String(); break;
                case "providerName": providerName = reader.String(); break;
                default: reader.Skip(); break;
            }
        }
        reader.EndObject();

        return new global::app.module.action.code.defaultoverride.@this
        {
            TypeName = typeName,
            ProviderName = providerName,
        };
    }
}
