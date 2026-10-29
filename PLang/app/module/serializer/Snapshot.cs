namespace app.module.serializer;

/// <summary>
/// A module read in the snapshot view: its key comes in (<c>{name}</c>) and the live module of that name comes out. A
/// key that names no module of the app, or holds a member no key of a module holds, is refused, thrown to the read
/// boundary.
/// </summary>
public sealed class Snapshot : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.View View => global::app.View.Snapshot;

    /// <summary>A module's key is an object.</summary>
    public bool Reads(global::app.type.format.TokenKind token) => token == global::app.type.format.TokenKind.Object;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind, global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        string? name = null;
        reader.BeginObject();
        while (reader.NextName(out var member))
            switch (member)
            {
                case "name": name = reader.String(); break;
                default: throw new global::app.error.DeclinedException(new global::app.error.@this(
                    $"key '{member}' isn't in this snapshot's format for a module", "SnapshotFormatOutdated", 400));
            }
        reader.EndObject();

        if (name == null) throw new global::app.error.DeclinedException(new global::app.error.@this(
            "a saved module's key names no module", "ModuleKeyIncomplete", 400));
        return ctx.Context.App.module.Named(name) ?? throw new global::app.error.DeclinedException(new global::app.error.@this(
            $"the saved module '{name}' is not one of the app's", "ModuleNotFound", 404));
    }
}
