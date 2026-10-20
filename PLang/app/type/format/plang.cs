namespace app.type.format;

/// <summary>
/// A type in plang's own output kind is its identity — <c>{name, kind?, strict?, template?}</c>: what a Data's type slot
/// and a property row's carry in a <c>.pr</c> and on the wire, whatever view. Written alone in bare json a type is its face
/// (<c>text</c>) instead, through its own Output. A kind is a type too, but a kind held as a value is its name, so it
/// writes itself.
/// </summary>
public sealed class plang : IOutput
{
    public System.Threading.Tasks.ValueTask Output(global::app.type.item.@this value, IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        if (value is global::app.type.kind.@this) return value.Output(writer, mode, context);
        ((global::app.type.@this)value).Write(writer);
        return default;
    }
}
