namespace app.type.kind.format;

/// <summary>
/// A kind in plang's own output kind is its name — a kind held as a value (<c>%x!type.kind%</c>) writes the word it is
/// (<c>md</c>, <c>decimal</c>), never a type's identity, though a kind is a type.
/// </summary>
public sealed class plang : global::app.type.format.IOutput
{
    public System.Threading.Tasks.ValueTask Output(global::app.type.item.@this value, global::app.type.format.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
        => value.Output(writer, mode, context);
}
