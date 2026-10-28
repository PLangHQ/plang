namespace app.serializer;

/// <summary>
/// The app read in the snapshot view: its key comes in (its name) and the app the run goes on in comes out — a snapshot
/// goes on only in the app that saved it. A name that is another app's is refused (<c>SnapshotAppMismatch</c>), thrown to
/// the read boundary.
/// </summary>
public sealed class Snapshot : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.View View => global::app.View.Snapshot;

    /// <summary>The app's key is its name.</summary>
    public bool Reads(global::app.type.format.TokenKind token) => token == global::app.type.format.TokenKind.String;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind, global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        var name = reader.String();
        var context = ctx.Context;
        // the app's name is its settings': read once the run's settings are, so the read finishes at once
        var named = context.App.Name(context).AsTask().GetAwaiter().GetResult();
        return string.Equals(named.ToString(), name, System.StringComparison.Ordinal)
            ? context.App
            : throw new global::app.error.DeclinedException(new global::app.error.@this(
                $"the snapshot names the app '{name}'; it goes on in '{named}'", "SnapshotAppMismatch", 409));
    }
}
