namespace app.actor.serializer;

/// <summary>
/// An actor read in the snapshot view: its key comes in (its name, <c>"user"</c>) and the actor of that name in the app
/// the run goes on in comes out. A name that is no actor's is refused, thrown to the read boundary.
/// </summary>
public sealed class Snapshot : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.View View => global::app.View.Snapshot;

    /// <summary>An actor's key is its name.</summary>
    public bool Reads(global::app.type.format.TokenKind token) => token == global::app.type.format.TokenKind.String;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind, global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        var name = reader.String();
        return System.Enum.TryParse<Name>(name, ignoreCase: true, out var named)
            ? ctx.Context.App.actor.list[named]
            : throw new global::app.error.DeclinedException(new global::app.error.@this(
                $"the saved actor '{name}' is no actor of this app", "ActorNotFound", 404));
    }
}
