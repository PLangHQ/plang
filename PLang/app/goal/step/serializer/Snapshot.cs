namespace app.goal.step.serializer;

/// <summary>
/// A step read in the snapshot view: its key comes in (<c>{goal, index}</c>) and the live step comes out — its goal read
/// by the goal's own snapshot reader, then the step at its index, numbered as its goal numbers them (goal-wide: a body's
/// steps on from the step that holds them). A key whose goal holds no step at the index is refused, thrown to the read
/// boundary.
/// </summary>
public sealed class Snapshot : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.View View => global::app.View.Snapshot;

    /// <summary>A key is an object; a string where a step stands names a variable holding one.</summary>
    public bool Reads(global::app.type.format.TokenKind token) => token == global::app.type.format.TokenKind.Object;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind, global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        var context = ctx.Context;
        global::app.goal.@this? goal = null;
        int? index = null;
        reader.BeginObject();
        while (reader.NextName(out var member))
            switch (member)
            {
                case "goal":
                    var type = context.App.type.list[typeof(global::app.goal.@this)];
                    goal = ctx.View.Reader(type, null, context).Read(ref reader, null, ctx) as global::app.goal.@this;
                    break;
                case "index": index = reader.Int(); break;
                default: reader.Skip(); break;
            }
        reader.EndObject();

        if (goal == null || index is not { } at)
            throw new global::app.error.DeclinedException(new global::app.error.@this(
                $"a saved step's key names {(goal == null ? "no goal" : "no index")}", "StepKeyIncomplete", 400));
        return goal[at] ?? throw new global::app.error.DeclinedException(new global::app.error.@this(
            $"the saved step {at} is not in goal '{goal.Name}'", "StepNotFound", 404));
    }
}
