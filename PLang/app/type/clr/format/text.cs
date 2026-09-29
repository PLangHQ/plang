namespace app.type.clr.format;

/// <summary>
/// A clr carrier's text form. A foreign host has no plain-text form, so on the <c>text</c>
/// channel it renders as its json — the carrier writes ITSELF through the json writer (the
/// same form it has on a json channel), captured as one bare string. An instance, held directly by
/// <see cref="app.type.clr.@this"/>'s own format map — no registry, no reflection.
/// </summary>
public sealed class text : global::app.type.format.IOutput
{
    public async System.Threading.Tasks.ValueTask Output(
        global::app.type.item.@this value, global::app.type.format.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        using var buffer = new System.IO.MemoryStream();
        await using (var utf8 = new System.Text.Json.Utf8JsonWriter(buffer))
        {
            var json = new global::app.type.item.kind.json.Writer(
                utf8, view: mode, emitsSchema: false);
            await value.Output(json, mode, context);
        }
        writer.String(System.Text.Encoding.UTF8.GetString(buffer.ToArray()));
    }
}
