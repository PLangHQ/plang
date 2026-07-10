namespace app.type.list.format;

/// <summary>
/// A list's text form. A list has no plain scalar form, so on the <c>text</c> channel it
/// renders as its json — the value writes ITSELF through the json writer (no STJ, no
/// converter), captured as one bare string. An instance, held directly by
/// <see cref="app.type.list.@this"/>'s own format map — no registry, no reflection.
/// </summary>
public sealed class text : global::app.channel.serializer.IOutput
{
    public async System.Threading.Tasks.ValueTask Output(
        global::app.type.item.@this value, global::app.channel.serializer.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        using var buffer = new System.IO.MemoryStream();
        await using (var utf8 = new System.Text.Json.Utf8JsonWriter(buffer))
        {
            var json = new global::app.channel.serializer.json.Writer(
                utf8, view: mode, renderers: context?.App.Type.Renderers, emitsSchema: false);
            await value.Output(json, mode, context);
        }
        writer.String(System.Text.Encoding.UTF8.GetString(buffer.ToArray()));
    }
}
