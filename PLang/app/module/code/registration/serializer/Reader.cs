namespace app.module.code.registration.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for <c>code.registration</c> — the
/// read-side mirror of the tagged write. Lazy like any content value: Restore reaches a row through
/// the typed ask, and the slice materializes here.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this(ctx.Context.App.type.list[new global::app.type.@this(typeof(global::app.module.code.registration.@this), kind), ctx.Context]);

        string providerKind = "", provider = "";

        reader.BeginObject();
        while (reader.NextName(out var name))
        {
            switch (name)
            {
                case "kind": providerKind = reader.String(); break;
                case "provider": provider = reader.String(); break;
                default: reader.Skip(); break;
            }
        }
        reader.EndObject();

        return new global::app.module.code.registration.@this(ctx.Context.App.type.list[typeof(global::app.module.code.registration.@this)])
        {
            Kind = providerKind,
            Provider = provider,
        };
    }
}
