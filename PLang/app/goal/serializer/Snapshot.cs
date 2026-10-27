namespace app.goal.serializer;

/// <summary>
/// A goal read in the snapshot view: its key comes in (<c>{name, path, hash}</c>, what the snapshot wrote for it) and the
/// live goal comes out — the goal of that name in the file its <c>.goal</c> is, found through the <c>.pr</c> that file is
/// built to, as long as it is the goal it was. The goal list loads that file before this read (the snapshot view's
/// <c>Load</c>), so the read finds it held. A key that names no goal held, or a goal changed since the save, is refused,
/// thrown to the read boundary.
/// </summary>
public sealed class Snapshot : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.View View => global::app.View.Snapshot;

    /// <summary>A goal's key is an object.</summary>
    public bool Reads(global::app.type.format.TokenKind token) => token == global::app.type.format.TokenKind.Object;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind, global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        var context = ctx.Context;
        string name = "", hash = "";
        global::app.type.item.path.@this? path = null;
        // each member read as the key wrote it: the name and hash as text, the .goal by path's own reader
        reader.BeginObject();
        while (reader.NextName(out var member))
            switch (member)
            {
                case "name": name = reader.String(); break;
                case "hash": hash = reader.String(); break;
                case "path":
                    var type = context.App.type.list[typeof(global::app.type.item.path.@this)];
                    path = ctx.View.Reader(type, null, context).Read(ref reader, null, ctx) as global::app.type.item.path.@this;
                    break;
                default: throw new global::app.error.DeclinedException(new global::app.error.@this(
                    $"key '{member}' isn't in this snapshot's format for a goal", "SnapshotFormatOutdated", 400));
            }
        reader.EndObject();

        // the goal the .goal's .pr holds, and the goal of that name in it: that one or one of its children
        if (path is not global::app.type.item.path.file.@this file)
            throw new global::app.error.DeclinedException(new global::app.error.@this(
                $"the saved goal '{name}' names no .goal to find it in", "GoalKeyIncomplete", 400));
        if (context.App.goal.list[file.Pr]?.Named(name) is not { } goal)
            throw new global::app.error.DeclinedException(new global::app.error.@this(
                $"the saved goal '{name}' is not in {file}", "GoalNotFound", 404));
        if (!string.Equals(goal.Hash ?? "", hash, StringComparison.OrdinalIgnoreCase))
            throw new global::app.error.DeclinedException(new global::app.error.@this(
                $"the saved goal '{name}' is not the one there now: saved hash {hash}, live {goal.Hash}.", "GoalHashMismatch", 409));
        return goal;
    }
}
