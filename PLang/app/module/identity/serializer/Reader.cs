namespace app.module.identity.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for
/// <see cref="app.module.identity.Identity"/> — the read-back mirror of
/// <see cref="app.module.identity.Identity.Output"/>. Streams the identity's fields off the
/// single decode pass into a typed <c>Identity</c> so a stored identity reconstructs as itself
/// (not a generic property-bag whose view-blind output would diverge from the signed hash).
/// Field set is the Store wire: {name, publicKey, privateKey, isDefault, isArchived, created}.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("identity", kind);
        reader.BeginObject();
        var id = new global::app.module.identity.Identity();
        while (reader.NextName(out var name))
        {
            switch (name.ToLowerInvariant())
            {
                case "name":       id.Name = reader.String(); break;
                case "publickey":  id.PublicKey = reader.String(); break;
                case "privatekey": id.PrivateKey = reader.String(); break;
                case "isdefault":  id.IsDefault = reader.Bool(); break;
                case "isarchived": id.IsArchived = reader.Bool(); break;
                case "created":    id.Created = reader.DateTimeOffset(); break;
                default:           reader.Skip(); break;
            }
        }
        reader.EndObject();
        return id;
    }
}
