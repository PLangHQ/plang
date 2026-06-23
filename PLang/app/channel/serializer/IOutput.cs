namespace app.channel.serializer;

/// <summary>
/// A value's serializer for ONE channel format — the per-format exception a type owns when
/// its neutral tokens don't fit a format (a container/object on the <c>text</c> channel has
/// no plain-text form, so it renders as json). A type holds its own small map of these,
/// instantiated DIRECTLY (no reflection, no central registry), keyed by format; the type's
/// own <c>Output</c> picks from it and otherwise writes its default token form inline.
/// </summary>
public interface IOutput
{
    System.Threading.Tasks.ValueTask Output(
        global::app.type.item.@this value, IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context);
}
