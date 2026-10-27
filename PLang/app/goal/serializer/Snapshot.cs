namespace app.goal.serializer;

/// <summary>
/// A goal read in the snapshot view: its key comes in (<c>{name, pr, hash}</c>, what the snapshot wrote for it) and the
/// live goal comes out — the goal of that name in what its <c>.pr</c> holds, as long as it is the goal it was. The
/// <c>.pr</c> is loaded before this read (the snapshot view's <c>Load</c>), so the read finds it held. A key that names
/// no goal held, or a goal changed since the save, is refused, thrown to the read boundary.
/// </summary>
public sealed class Snapshot : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.View View => global::app.View.Snapshot;

    /// <summary>A key is an object; a string where a goal stands names a variable holding one.</summary>
    public bool Reads(global::app.type.format.TokenKind token) => token == global::app.type.format.TokenKind.Object;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind, global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        var context = ctx.Context;
        string name = "", hash = "";
        global::app.type.item.file.@this? pr = null;
        // each member read as the key wrote it: the name and hash as text, the .pr by its own type's reader
        reader.BeginObject();
        while (reader.NextName(out var member))
            switch (member)
            {
                case "name": name = reader.String(); break;
                case "hash": hash = reader.String(); break;
                case "pr":
                    var file = context.App.type.list[typeof(global::app.type.item.file.@this)];
                    pr = ctx.View.Reader(file, null, context).Read(ref reader, null, ctx) as global::app.type.item.file.@this;
                    break;
                default: throw Refused($"key '{member}' isn't in this snapshot's format for a goal", "SnapshotFormatOutdated", 400);
            }
        reader.EndObject();

        // the goal its .pr holds, and the goal of that name in it: that one or one of its children
        if (pr == null)
            throw Refused($"the saved goal '{name}' names no .pr to find it in", "GoalKeyIncomplete", 400);
        if (context.App.goal.list[pr.Path]?.Named(name) is not { } goal)
            throw Refused($"the saved goal '{name}' is not in {pr.Path}", "GoalNotFound", 404);
        if (!string.Equals(goal.Hash ?? "", hash, StringComparison.OrdinalIgnoreCase))
            throw Refused($"the saved goal '{name}' is not the one there now: saved hash {hash}, live {goal.Hash}.", "GoalHashMismatch", 409);
        return goal;
    }

    private global::app.error.DeclinedException Refused(string message, string key, int status)
        => new(new global::app.error.@this(message, key, status));
}
