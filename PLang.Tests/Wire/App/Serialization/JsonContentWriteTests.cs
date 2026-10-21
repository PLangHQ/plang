namespace PLang.Tests.App.Serialization;

// json content's characters already are json: a json document pastes them; a text beside it is quoted, bytes are
// base64; on a text channel json content is its characters as written.
public class JsonContentWriteTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();
    private global::app.actor.context.@this Ctx => app.actor.list.User.Context;

    private const string Content = "{ \"port\":  8080,\n  \"hosts\": [\"a\"] }";

    private global::app.data.@this Json(object raw)
        => new("", raw, new global::app.type.@this("item", "json"), context: Ctx);

    private async Task<string> Encoded(string mime, global::app.data.@this data)
    {
        using var stream = new System.IO.MemoryStream();
        await (await Ctx.App.type.list.Mime(mime).Encode(stream, data, Ctx)).IsSuccess();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task JsonContent_InsideAJsonDocument_IsPasted_ATextQuoted_BytesBase64(bool asBytes)
    {
        var dict = global::app.type.item.@this.Create(new System.Collections.Generic.Dictionary<string, object?>
        {
            ["json"] = Json(asBytes ? System.Text.Encoding.UTF8.GetBytes(Content) : Content),
            ["text"] = Content,
            ["bytes"] = new byte[] { 1, 2, 3 },
        }, Ctx);

        var written = await Encoded("application/json", new global::app.data.@this("", dict, context: Ctx));

        await Assert.That(written).Contains("\"json\":" + Content);
        await Assert.That(written).Contains("\"text\":\"{ \\u0022port\\u0022");
        await Assert.That(written).Contains("\"bytes\":\"AQID\"");
    }

    [Test]
    public async Task JsonContent_OnATextChannel_IsItsCharactersAsWritten()
        => await Assert.That(await Encoded("text/plain", Json(Content))).IsEqualTo(Content);
}
