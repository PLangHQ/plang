namespace app.type.dict.format;

/// <summary>
/// A dict's text form. A dict has no plain scalar form, so on the <c>text</c> channel it
/// renders as its json (its own <c>[JsonConverter]</c>), written as one bare string. An
/// instance, held directly by <see cref="app.type.dict.@this"/>'s own format map — no
/// registry, no reflection.
/// </summary>
public sealed class text : global::app.channel.serializer.IOutput
{
    public System.Threading.Tasks.ValueTask Output(
        global::app.type.item.@this value, global::app.channel.serializer.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        writer.String(System.Text.Json.JsonSerializer.Serialize(value, value.GetType()));
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }
}
