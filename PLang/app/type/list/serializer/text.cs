namespace app.type.list.serializer;

/// <summary>
/// Text-format write override for <see cref="app.type.list.@this"/>. A list has no plain
/// scalar form, so on the <c>text</c> channel it renders as its json (its own
/// <c>[JsonConverter]</c>), written as one bare string through the text writer. Discovered
/// by <c>data.Output</c> via the <c>serializer/&lt;format&gt;</c> convention; the list
/// itself never branches on format.
/// </summary>
public static class text
{
    public static System.Threading.Tasks.ValueTask Output(
        global::app.type.item.@this value, global::app.channel.serializer.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        writer.String(System.Text.Json.JsonSerializer.Serialize(value, value.GetType()));
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }
}
