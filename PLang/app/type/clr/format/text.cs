namespace app.type.clr.format;

/// <summary>
/// A clr carrier's text form. A foreign host has no plain-text form, so on the <c>text</c>
/// channel it renders as json (its host serialized). An instance, held directly by
/// <see cref="app.type.clr.@this"/>'s own format map — no registry, no reflection.
/// </summary>
public sealed class text : global::app.channel.serializer.IOutput
{
    public System.Threading.Tasks.ValueTask Output(
        global::app.type.item.@this value, global::app.channel.serializer.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        writer.String(System.Text.Json.JsonSerializer.Serialize(((global::app.type.clr.@this)value).Value));
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }
}
