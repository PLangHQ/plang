namespace app.type.format;

/// <summary>
/// A pair: what a value of one type looks like in an output kind, when it isn't the value's own write — a list in
/// formal is a list of arguments, a type in plang its identity. The output kind holds its pairs
/// (<see cref="global::app.type.kind.@this.pair"/>) and writes every value through them; a pair lives in its input type's
/// folder, <c>&lt;type folder&gt;/format/&lt;output kind&gt;.cs</c>, found when the kind is first asked. A value never asks what
/// its writer is. A value writing a member writes it through the door (<c>writer.Kind.pair.Output(member, …)</c>), never
/// <c>member.Output(writer, …)</c> directly — that skips the member's pair.
/// </summary>
public interface IOutput
{
    System.Threading.Tasks.ValueTask Output(
        global::app.type.item.@this value, IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context);
}
